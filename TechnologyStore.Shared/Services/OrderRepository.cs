using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Interfaces;
using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TechnologyStore.Shared.Services;

/// <summary>
/// Repository for order data access
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly string _connectionString;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));
            
        _connectionString = connectionString;
        _logger = AppLogger.CreateLogger<OrderRepository>();
    }

    private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    public async Task<Order> CreateOrderAsync(Order order)
    {
        using var db = CreateConnection();
        await db.OpenAsync();
        using var transaction = await db.BeginTransactionAsync();

        try
        {
            // Insert order
            var orderSql = @"
                INSERT INTO orders (order_number, customer_id, status, subtotal, tax, total, notes, pickup_date)
                VALUES (@OrderNumber, @CustomerId, @Status::order_status, @Subtotal, @Tax, @Total, @Notes, @PickupDate)
                RETURNING id;";
            
            order.Id = await db.ExecuteScalarAsync<int>(orderSql, new
            {
                order.OrderNumber,
                order.CustomerId,
                order.Status,
                order.Subtotal,
                order.Tax,
                order.Total,
                order.Notes,
                order.PickupDate
            }, transaction);

            // Insert order items
            var itemSql = @"
                INSERT INTO order_items (order_id, product_id, product_name, unit_price, quantity, line_total)
                VALUES (@OrderId, @ProductId, @ProductName, @UnitPrice, @Quantity, @LineTotal);";
            
            foreach (var item in order.Items)
            {
                item.OrderId = order.Id;
                await db.ExecuteAsync(itemSql, item, transaction);
            }

            await transaction.CommitAsync();
            
            _logger.LogInformation("Created order {OrderNumber} for customer {CustomerId}", order.OrderNumber, order.CustomerId);
            return order;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Order?> GetByIdAsync(int orderId)
    {
        using var db = CreateConnection();
        
        var orderSql = @"
            SELECT id as Id, order_number as OrderNumber, customer_id as CustomerId,
                   status::text as Status, subtotal as Subtotal, tax as Tax, total as Total,
                   notes as Notes, pickup_date as PickupDate, created_at as CreatedAt,
                   updated_at as UpdatedAt, completed_at as CompletedAt
            FROM orders
            WHERE id = @OrderId;";
        
        var order = await db.QueryFirstOrDefaultAsync<Order>(orderSql, new { OrderId = orderId });
        if (order == null) return null;

        var itemsSql = @"
            SELECT id as Id, order_id as OrderId, product_id as ProductId,
                   product_name as ProductName, unit_price as UnitPrice,
                   quantity as Quantity, line_total as LineTotal
            FROM order_items
            WHERE order_id = @OrderId;";
        
        order.Items = (await db.QueryAsync<OrderItem>(itemsSql, new { OrderId = orderId })).ToList();
        
        return order;
    }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber)
    {
        using var db = CreateConnection();
        
        var orderSql = @"
            SELECT id as Id, order_number as OrderNumber, customer_id as CustomerId,
                   status::text as Status, subtotal as Subtotal, tax as Tax, total as Total,
                   notes as Notes, pickup_date as PickupDate, created_at as CreatedAt,
                   updated_at as UpdatedAt, completed_at as CompletedAt
            FROM orders
            WHERE order_number = @OrderNumber;";
        
        var order = await db.QueryFirstOrDefaultAsync<Order>(orderSql, new { OrderNumber = orderNumber });
        if (order == null) return null;

        var itemsSql = @"
            SELECT id as Id, order_id as OrderId, product_id as ProductId,
                   product_name as ProductName, unit_price as UnitPrice,
                   quantity as Quantity, line_total as LineTotal
            FROM order_items
            WHERE order_id = @OrderId;";
        
        order.Items = (await db.QueryAsync<OrderItem>(itemsSql, new { OrderId = order.Id })).ToList();
        
        return order;
    }

    public async Task<IEnumerable<Order>> GetByCustomerIdAsync(int customerId)
    {
        using var db = CreateConnection();
        
        var orderSql = @"
            SELECT id as Id, order_number as OrderNumber, customer_id as CustomerId,
                   status::text as Status, subtotal as Subtotal, tax as Tax, total as Total,
                   notes as Notes, pickup_date as PickupDate, created_at as CreatedAt,
                   updated_at as UpdatedAt, completed_at as CompletedAt
            FROM orders
            WHERE customer_id = @CustomerId
            ORDER BY created_at DESC;";
        
        var orders = (await db.QueryAsync<Order>(orderSql, new { CustomerId = customerId })).ToList();
        
        // Load items for each order
        foreach (var order in orders)
        {
            var itemsSql = @"
                SELECT id as Id, order_id as OrderId, product_id as ProductId,
                       product_name as ProductName, unit_price as UnitPrice,
                       quantity as Quantity, line_total as LineTotal
                FROM order_items
                WHERE order_id = @OrderId;";
            
            order.Items = (await db.QueryAsync<OrderItem>(itemsSql, new { OrderId = order.Id })).ToList();
        }
        
        return orders;
    }

    public async Task<IEnumerable<Order>> GetAllOrdersAsync(string? statusFilter = null)
    {
        using var db = CreateConnection();
        
        var orderSql = @"
            SELECT o.id as Id, o.order_number as OrderNumber, o.customer_id as CustomerId,
                   o.status::text as Status, o.subtotal as Subtotal, o.tax as Tax, o.total as Total,
                   o.notes as Notes, o.pickup_date as PickupDate, o.created_at as CreatedAt,
                   o.updated_at as UpdatedAt, o.completed_at as CompletedAt,
                   c.email as CustomerEmail, c.full_name as CustomerName
            FROM orders o
            JOIN customers c ON o.customer_id = c.id
            WHERE (@StatusFilter IS NULL OR o.status::text = @StatusFilter)
            ORDER BY o.created_at DESC;";
        
        var orders = (await db.QueryAsync<Order>(orderSql, new { StatusFilter = statusFilter })).ToList();
        
        return orders;
    }

    public async Task UpdateStatusAsync(int orderId, string status)
    {
        using var db = CreateConnection();
        
        var sql = @"
            UPDATE orders 
            SET status = @Status::order_status, 
                updated_at = CURRENT_TIMESTAMP,
                completed_at = CASE WHEN @Status IN ('COMPLETED', 'CANCELLED') THEN CURRENT_TIMESTAMP ELSE completed_at END
            WHERE id = @OrderId;";
        
        await db.ExecuteAsync(sql, new { OrderId = orderId, Status = status });
        _logger.LogInformation("Updated order {OrderId} status to {Status}", orderId, status);
    }

    public async Task<string> GenerateOrderNumberAsync()
    {
        using var db = CreateConnection();
        
        // Get the next sequence number for today
        var year = DateTime.Now.Year;
        var sql = @"
            SELECT COALESCE(MAX(CAST(SUBSTRING(order_number FROM 10) AS INTEGER)), 0) + 1
            FROM orders
            WHERE order_number LIKE @Pattern;";
        
        var pattern = $"ORD-{year}-%";
        var nextNumber = await db.ExecuteScalarAsync<int>(sql, new { Pattern = pattern });
        
        return $"ORD-{year}-{nextNumber:D5}";
    }
}
