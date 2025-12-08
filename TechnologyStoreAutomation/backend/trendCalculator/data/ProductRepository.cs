using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql; // PostgreSQL Driver

namespace TechnologyStoreAutomation.backend.trendCalculator.data;

public class ProductRepository : IProductRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ProductRepository> _logger;
    
    // Retry configuration
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    public ProductRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString), "Connection string cannot be null or empty");
            
        _connectionString = connectionString;
        _logger = AppLogger.CreateLogger<ProductRepository>();
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// Executes a database operation with retry logic for transient failures
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName)
    {
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (NpgsqlException ex) when (IsTransientError(ex) && attempt < MaxRetries)
            {
                lastException = ex;
                _logger.LogWarning(ex, 
                    "{Operation} failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms...",
                    operationName, attempt, MaxRetries, RetryDelay.TotalMilliseconds);
                await Task.Delay(RetryDelay * attempt).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Operation} failed with non-transient error", operationName);
                throw;
            }
        }
        
        throw lastException ?? new InvalidOperationException($"{operationName} failed after {MaxRetries} attempts");
    }

    /// <summary>
    /// Determines if a PostgreSQL exception is transient (can be retried)
    /// </summary>
    private static bool IsTransientError(NpgsqlException ex)
    {
        // Common transient PostgreSQL error codes
        var transientCodes = new[] { "08000", "08003", "08006", "08001", "08004", "57P01", "57P02", "57P03" };
        return ex.SqlState != null && transientCodes.Contains(ex.SqlState);
    }

    // --- 1. NIGHTLY JOB LOGIC (The Heavy Lifting) ---

    /// <summary>
    /// Calculates the 'Closing Stock' and 'Total Sold' for yesterday based on the Transaction Ledger.
    /// This creates the permanent snapshot so the UI doesn't have to calculate millions of rows.
    /// </summary>
    public async Task GenerateDailySnapshotAsync(DateTime dateToProcess)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using (var db = CreateConnection())
            {
                _logger.LogInformation("Generating daily snapshot for {Date}", dateToProcess.ToShortDateString());
                
                var sql = @"
                    INSERT INTO daily_summaries (summary_date, product_id, closing_stock, total_sold)
                    SELECT 
                        @Date,
                        p.id,
                        COALESCE(SUM(t.quantity_change), 0) as closing_stock,
                        COALESCE(ABS(SUM(CASE WHEN t.quantity_change < 0 AND t.transaction_date >= @Date THEN t.quantity_change ELSE 0 END)), 0) as sold_today
                    FROM products p
                    LEFT JOIN inventory_transactions t ON p.id = t.product_id
                    GROUP BY p.id
                    ON CONFLICT (summary_date, product_id) DO UPDATE 
                    SET closing_stock = EXCLUDED.closing_stock, total_sold = EXCLUDED.total_sold;";

                await db.ExecuteAsync(sql, new { Date = dateToProcess }).ConfigureAwait(false);
                
                _logger.LogInformation("Daily snapshot generated successfully for {Date}", dateToProcess.ToShortDateString());
                return true;
            }
        }, nameof(GenerateDailySnapshotAsync)).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the lifecycle status of a product (Active -> Legacy -> Obsolete)
    /// </summary>
    public async Task UpdateProductPhaseAsync(int productId, string newPhase, string reason)
    {
        if (string.IsNullOrWhiteSpace(newPhase))
            throw new ArgumentNullException(nameof(newPhase), "New phase cannot be null or empty");
            
        await ExecuteWithRetryAsync(async () =>
        {
            using (var db = CreateConnection())
            {
                _logger.LogInformation("Updating product {ProductId} to phase {Phase}. Reason: {Reason}", 
                    productId, newPhase, reason);
                    
                var sql = @"
                    UPDATE products 
                    SET lifecycle_phase = @Phase::lifecycle_phase_type, 
                        last_updated = CURRENT_TIMESTAMP 
                    WHERE id = @Id;

                    INSERT INTO lifecycle_audit_log (product_id, new_phase, reason)
                    VALUES (@Id, @Phase::lifecycle_phase_type, @Reason);";

                await db.ExecuteAsync(sql, new { Id = productId, Phase = newPhase, Reason = reason }).ConfigureAwait(false);
                return true;
            }
        }, nameof(UpdateProductPhaseAsync)).ConfigureAwait(false);
    }

    // --- 2. SALES TRACKING METHODS ---

    /// <summary>
    /// Records a new sale and updates product stock
    /// </summary>
    public async Task<int> RecordSaleAsync(int productId, int quantitySold, decimal totalAmount, DateTime? saleDate = null)
    {
        using (var db = CreateConnection())
        {
            db.Open();
            using (var transaction = db.BeginTransaction())
            {
                try
                {
                    var date = saleDate ?? DateTime.Today;
                    var parameters = new { ProductId = productId, Quantity = quantitySold, Amount = totalAmount, Date = date };

                    // Insert sale transaction and get the ID
                    var insertSql = @"
                        INSERT INTO sales_transactions (product_id, quantity_sold, total_amount, sale_date)
                        VALUES (@ProductId, @Quantity, @Amount, @Date)
                        RETURNING id;";
                    var saleId = await db.ExecuteScalarAsync<int>(insertSql, parameters, transaction);

                    // Update product stock
                    var updateStockSql = @"
                        UPDATE products 
                        SET current_stock = current_stock - @Quantity,
                            last_updated = CURRENT_TIMESTAMP
                        WHERE id = @ProductId;";
                    await db.ExecuteAsync(updateStockSql, parameters, transaction);

                    // Record inventory transaction
                    var inventorySql = @"
                        INSERT INTO inventory_transactions (product_id, quantity_change, transaction_type, transaction_date)
                        VALUES (@ProductId, -@Quantity, 'SALE', @Date);";
                    await db.ExecuteAsync(inventorySql, parameters, transaction);

                    transaction.Commit();
                    return saleId;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Get sales history for a specific product
    /// </summary>
    public async Task<IEnumerable<SalesTransaction>> GetSalesHistoryAsync(int productId, int days = 30)
    {
        using (var db = CreateConnection())
        {
            var sql = @"
                SELECT id as Id, product_id as ProductId, quantity_sold as QuantitySold, 
                       total_amount as TotalAmount, sale_date as SaleDate, created_at as CreatedAt, notes as Notes
                FROM sales_transactions
                WHERE product_id = @ProductId 
                  AND sale_date >= CURRENT_DATE - @Days::INTEGER
                ORDER BY sale_date DESC;";
            
            return await db.QueryAsync<SalesTransaction>(sql, new { ProductId = productId, Days = days });
        }
    }

    /// <summary>
    /// Get all products with basic info
    /// </summary>
    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            using (var db = CreateConnection())
            {
                var sql = @"
                    SELECT id as Id, name as Name, sku as Sku, category as Category, 
                           unit_price as UnitPrice, current_stock as CurrentStock, 
                           lifecycle_phase::text as LifecyclePhase, 
                           successor_product_id as SuccessorProductId,
                           created_at as CreatedAt, last_updated as LastUpdated
                    FROM products
                    ORDER BY name;";
            
                return await db.QueryAsync<Product>(sql).ConfigureAwait(false);
            }
        }, nameof(GetAllProductsAsync)).ConfigureAwait(false);
    }

    /// <summary>
    /// Get products with low stock (below threshold)
    /// </summary>
    public async Task<IEnumerable<ProductDashboardDto>> GetDashboardDataAsync()
    {
        using (var db = CreateConnection())
        {
            // 1. Fetch all products (Explicit mapping ensures data isn't null)
            var productsSql = @"
            SELECT id as Id, name as Name, sku as Sku, category as Category, 
                   unit_price as UnitPrice, current_stock as CurrentStock, 
                   lifecycle_phase::text as LifecyclePhase, 
                   successor_product_id as SuccessorProductId,
                   created_at as CreatedAt, last_updated as LastUpdated
            FROM products 
            ORDER BY category";
            var products = await db.QueryAsync<Product>(productsSql);

            // 2. Fetch RECENT sales for ALL items (Explicit mapping here too)
            var salesSql = @"
            SELECT id as Id, product_id as ProductId, quantity_sold as QuantitySold, 
                   total_amount as TotalAmount, sale_date as SaleDate
            FROM sales_transactions 
            WHERE sale_date >= CURRENT_DATE - INTERVAL '30 days'";
            var allSales = await db.QueryAsync<SalesTransaction>(salesSql);

            // 3. Create the lookup (O(1) access)
            var salesLookup = allSales.GroupBy(s => s.ProductId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var dashboardData = new List<ProductDashboardDto>();

            foreach (var product in products)
            {
                var history = salesLookup.ContainsKey(product.Id)
                    ? salesLookup[product.Id]
                    : new List<SalesTransaction>();

                // The Logic Engine does the work
                var analysis = TrendCalculator.AnalyzeProduct(product, history);
                var rec = RecommendationEngine.GetRecommendation(analysis, product.LifecyclePhase);

                dashboardData.Add(new ProductDashboardDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Phase = product.LifecyclePhase,
                    Recommendation = rec,
                    CurrentStock = product.CurrentStock,
                    SalesLast7Days = (int)Math.Round(analysis.DailySalesAverage * 7),
                    RunwayDays = analysis.RunwayDays
                });
            }

            return dashboardData;
        }
    }
}