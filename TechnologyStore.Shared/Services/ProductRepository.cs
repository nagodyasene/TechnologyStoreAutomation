using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Interfaces;
using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TechnologyStore.Shared.Services;

public class ProductRepository : IProductRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ProductRepository> _logger;
    private readonly ITrendCalculator _trendCalculator;
    private readonly IRecommendationEngine _recommendationEngine;

    // Retry configuration
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    public ProductRepository(
        string connectionString,
        ITrendCalculator trendCalculator,
        IRecommendationEngine recommendationEngine)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString), "Connection string cannot be null or empty");

        _connectionString = connectionString;
        _trendCalculator = trendCalculator ?? throw new ArgumentNullException(nameof(trendCalculator));
        _recommendationEngine = recommendationEngine ?? throw new ArgumentNullException(nameof(recommendationEngine));
        _logger = AppLogger.CreateLogger<ProductRepository>();
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// Executes a database operation with retry logic for transient failures
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName)
    {
        Exception lastException = new InvalidOperationException($"Operation '{operationName}' failed after {MaxRetries} attempts");

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
                throw new InvalidOperationException($"Operation '{operationName}' failed with non-transient error", ex);
            }
        }

        throw lastException;
    }

    /// <summary>
    /// Determines if a PostgreSQL exception is transient (can be retried)
    /// </summary>
    private static bool IsTransientError(NpgsqlException ex)
    {
        var transientCodes = new[] { "08000", "08003", "08006", "08001", "08004", "57P01", "57P02", "57P03" };
        return ex.SqlState != null && transientCodes.Contains(ex.SqlState);
    }

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
                        -- Closing stock = current stock at end of snapshot date
                        -- Calculate as: initial stock (0) + sum of all transactions up to snapshot date
                        COALESCE((
                            SELECT COALESCE(SUM(t.quantity_change), 0)
                            FROM inventory_transactions t
                            WHERE t.product_id = p.id 
                              AND t.transaction_date::date <= @Date
                        ), 0) as closing_stock,
                        -- Total sold = sum of negative quantity changes on the snapshot date only
                        COALESCE(ABS(SUM(CASE 
                            WHEN t.quantity_change < 0 
                             AND t.transaction_date::date = @Date 
                            THEN t.quantity_change 
                            ELSE 0 
                        END)), 0) as total_sold
                    FROM products p
                    LEFT JOIN inventory_transactions t ON p.id = t.product_id 
                        AND t.transaction_date::date = @Date
                    GROUP BY p.id
                    ON CONFLICT (summary_date, product_id) DO UPDATE 
                    SET closing_stock = EXCLUDED.closing_stock, total_sold = EXCLUDED.total_sold;";

                await db.ExecuteAsync(sql, new { Date = dateToProcess }).ConfigureAwait(false);

                _logger.LogInformation("Daily snapshot generated successfully for {Date}", dateToProcess.ToShortDateString());
                return true;
            }
        }, nameof(GenerateDailySnapshotAsync)).ConfigureAwait(false);
    }

    public async Task UpdateProductPhaseAsync(int productId, string newPhase, string reason)
    {
        if (string.IsNullOrWhiteSpace(newPhase))
            throw new ArgumentNullException(nameof(newPhase), "New phase cannot be null or empty");

        await ExecuteWithRetryAsync(async () =>
        {
            using (var db = CreateConnection())
            {
                if (db is NpgsqlConnection npgsqlConn)
                {
                    await npgsqlConn.OpenAsync().ConfigureAwait(false);
                    using (var transaction = await npgsqlConn.BeginTransactionAsync().ConfigureAwait(false))
                    {
                        try
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

                            await db.ExecuteAsync(sql, new { Id = productId, Phase = newPhase, Reason = reason }, transaction).ConfigureAwait(false);
                            await transaction.CommitAsync().ConfigureAwait(false);
                            return true;
                        }
                        catch
                        {
                            await transaction.RollbackAsync().ConfigureAwait(false);
                            throw;
                        }
                    }
                }
                else
                {
                    db.Open();
                    using (var transaction = db.BeginTransaction())
                    {
                        try
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

                            await db.ExecuteAsync(sql, new { Id = productId, Phase = newPhase, Reason = reason }, transaction).ConfigureAwait(false);
                            transaction.Commit();
                            return true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
        }, nameof(UpdateProductPhaseAsync)).ConfigureAwait(false);
    }

    public async Task<int> RecordSaleAsync(int productId, int quantitySold, decimal totalAmount, DateTime? saleDate = null)
    {
        using (var db = CreateConnection())
        {
            if (db is NpgsqlConnection npgsqlConn)
            {
                await npgsqlConn.OpenAsync().ConfigureAwait(false);
                using (var transaction = await npgsqlConn.BeginTransactionAsync().ConfigureAwait(false))
                {
                    try
                    {
                        var date = saleDate ?? DateTime.Today;
                        var parameters = new { ProductId = productId, Quantity = quantitySold, Amount = totalAmount, Date = date };

                        var insertSql = @"
                            INSERT INTO sales_transactions (product_id, quantity_sold, total_amount, sale_date)
                            VALUES (@ProductId, @Quantity, @Amount, @Date)
                            RETURNING id;";
                        var saleId = await db.ExecuteScalarAsync<int>(insertSql, parameters, transaction).ConfigureAwait(false);

                        var updateStockSql = @"
                            UPDATE products 
                            SET current_stock = current_stock - @Quantity,
                                last_updated = CURRENT_TIMESTAMP
                            WHERE id = @ProductId;";
                        await db.ExecuteAsync(updateStockSql, parameters, transaction).ConfigureAwait(false);

                        var inventorySql = @"
                            INSERT INTO inventory_transactions (product_id, quantity_change, transaction_type, transaction_date)
                            VALUES (@ProductId, -@Quantity, 'SALE', @Date);";
                        await db.ExecuteAsync(inventorySql, parameters, transaction).ConfigureAwait(false);

                        await transaction.CommitAsync().ConfigureAwait(false);
                        return saleId;
                    }
                    catch
                    {
                        await transaction.RollbackAsync().ConfigureAwait(false);
                        throw;
                    }
                }
            }
            else
            {
                db.Open();
                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        var date = saleDate ?? DateTime.Today;
                        var parameters = new { ProductId = productId, Quantity = quantitySold, Amount = totalAmount, Date = date };

                        var insertSql = @"
                            INSERT INTO sales_transactions (product_id, quantity_sold, total_amount, sale_date)
                            VALUES (@ProductId, @Quantity, @Amount, @Date)
                            RETURNING id;";
                        var saleId = await db.ExecuteScalarAsync<int>(insertSql, parameters, transaction).ConfigureAwait(false);

                        var updateStockSql = @"
                            UPDATE products 
                            SET current_stock = current_stock - @Quantity,
                                last_updated = CURRENT_TIMESTAMP
                            WHERE id = @ProductId;";
                        await db.ExecuteAsync(updateStockSql, parameters, transaction).ConfigureAwait(false);

                        var inventorySql = @"
                            INSERT INTO inventory_transactions (product_id, quantity_change, transaction_type, transaction_date)
                            VALUES (@ProductId, -@Quantity, 'SALE', @Date);";
                        await db.ExecuteAsync(inventorySql, parameters, transaction).ConfigureAwait(false);

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
    }

    public async Task<IEnumerable<SalesTransaction>> GetSalesHistoryAsync(int productId, int days = 30)
    {
        using (var db = CreateConnection())
        {
            var sql = @"
                SELECT id as Id, product_id as ProductId, quantity_sold as QuantitySold, 
                       total_amount as TotalAmount, sale_date::timestamp as SaleDate, created_at as CreatedAt, notes as Notes
                FROM sales_transactions
                WHERE product_id = @ProductId 
                  AND sale_date >= CURRENT_DATE - @Days::INTEGER
                ORDER BY sale_date DESC;";

            return await db.QueryAsync<SalesTransaction>(sql, new { ProductId = productId, Days = days });
        }
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        return await GetAllAsync();
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
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
                           supplier_id as SupplierId,
                           created_at as CreatedAt, last_updated as LastUpdated
                    FROM products
                    ORDER BY name;";

                return await db.QueryAsync<Product>(sql).ConfigureAwait(false);
            }
        }, nameof(GetAllAsync)).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ProductDashboardDto>> GetDashboardDataAsync()
    {
        using (var db = CreateConnection())
        {
            var productsSql = @"
            SELECT id as Id, name as Name, sku as Sku, category as Category, 
                   unit_price as UnitPrice, current_stock as CurrentStock, 
                   lifecycle_phase::text as LifecyclePhase, 
                   successor_product_id as SuccessorProductId,
                   created_at as CreatedAt, last_updated as LastUpdated
            FROM products 
            ORDER BY category";
            var products = await db.QueryAsync<Product>(productsSql);

            var salesSql = @"
            SELECT id as Id, product_id as ProductId, quantity_sold as QuantitySold, 
                   total_amount as TotalAmount, sale_date::timestamp as SaleDate
            FROM sales_transactions 
            WHERE sale_date >= CURRENT_DATE - INTERVAL '30 days'";
            var allSales = await db.QueryAsync<SalesTransaction>(salesSql);

            var salesLookup = allSales.GroupBy(s => s.ProductId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var dashboardData = new List<ProductDashboardDto>();

            foreach (var product in products)
            {
                var history = salesLookup.ContainsKey(product.Id)
                    ? salesLookup[product.Id]
                    : new List<SalesTransaction>();

                var analysis = _trendCalculator.AnalyzeProduct(product, history);
                var rec = _recommendationEngine.GenerateRecommendation(analysis, product.LifecyclePhase);

                dashboardData.Add(new ProductDashboardDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Category = product.Category,
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

    public async Task<Product?> GetByIdAsync(int productId)
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
                WHERE id = @ProductId;";

            return await db.QueryFirstOrDefaultAsync<Product>(sql, new { ProductId = productId });
        }
    }

    public async Task<IEnumerable<Product>> GetAvailableProductsAsync()
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
                WHERE lifecycle_phase IN ('ACTIVE', 'LEGACY')
                  AND current_stock > 0
                ORDER BY category, name;";

            return await db.QueryAsync<Product>(sql);
        }
    }

    public async Task<bool> ReserveStockAsync(int productId, int quantity)
    {
        using (var db = CreateConnection())
        {
            // Check stock first
            var checkSql = "SELECT current_stock FROM products WHERE id = @ProductId;";
            var currentStock = await db.ExecuteScalarAsync<int>(checkSql, new { ProductId = productId });
            
            if (currentStock < quantity)
            {
                return false;
            }

            // Reserve stock
            var updateSql = @"
                UPDATE products 
                SET current_stock = current_stock - @Quantity,
                    last_updated = CURRENT_TIMESTAMP
                WHERE id = @ProductId AND current_stock >= @Quantity;";
            
            var affectedRows = await db.ExecuteAsync(updateSql, new { ProductId = productId, Quantity = quantity });
            return affectedRows > 0;
        }
    }

    public async Task ReleaseStockAsync(int productId, int quantity)
    {
        using (var db = CreateConnection())
        {
            var sql = @"
                UPDATE products 
                SET current_stock = current_stock + @Quantity,
                    last_updated = CURRENT_TIMESTAMP
                WHERE id = @ProductId;";
            
            await db.ExecuteAsync(sql, new { ProductId = productId, Quantity = quantity });
        }
    }
}
