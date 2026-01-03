using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Services;

/// <summary>
/// PostgreSQL implementation of IPurchaseOrderRepository
/// </summary>
public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PurchaseOrderRepository> _logger;

    public PurchaseOrderRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = AppLogger.CreateLogger<PurchaseOrderRepository>();
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    /// <inheritdoc />
    public async Task<PurchaseOrder> CreateAsync(PurchaseOrder order)
    {
        using var db = CreateConnection();
        await db.OpenAsync();
        using var transaction = await db.BeginTransactionAsync();

        try
        {
            // Insert the purchase order
            const string orderSql = @"
                INSERT INTO purchase_orders (order_number, supplier_id, status, total_amount, notes, expected_delivery_date)
                VALUES (@OrderNumber, @SupplierId, @Status::purchase_order_status, @TotalAmount, @Notes, @ExpectedDeliveryDate)
                RETURNING id, created_at";

            var result = await db.QuerySingleAsync<(int Id, DateTime CreatedAt)>(orderSql, new
            {
                order.OrderNumber,
                order.SupplierId,
                Status = order.Status.ToString().ToUpper(),
                order.TotalAmount,
                order.Notes,
                order.ExpectedDeliveryDate
            }, transaction);

            order.Id = result.Id;
            order.CreatedAt = result.CreatedAt;

            // Insert line items
            if (order.Items.Any())
            {
                const string itemSql = @"
                    INSERT INTO purchase_order_items (purchase_order_id, product_id, product_name, product_sku, quantity, unit_cost)
                    VALUES (@PurchaseOrderId, @ProductId, @ProductName, @ProductSku, @Quantity, @UnitCost)
                    RETURNING id";

                foreach (var item in order.Items)
                {
                    item.PurchaseOrderId = order.Id;
                    item.Id = await db.ExecuteScalarAsync<int>(itemSql, item, transaction);
                }
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Created purchase order: {OrderNumber} (ID: {Id}) with {ItemCount} items",
                order.OrderNumber, order.Id, order.Items.Count);

            return order;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PurchaseOrder?> GetByIdAsync(int id)
    {
        using var db = CreateConnection();

        const string orderSql = @"
            SELECT po.id as Id, po.order_number as OrderNumber, po.supplier_id as SupplierId,
                   po.status::text as Status, po.total_amount as TotalAmount, po.notes as Notes,
                   po.created_at as CreatedAt, po.approved_at as ApprovedAt, 
                   po.approved_by_user_id as ApprovedByUserId,
                   po.sent_at as SentAt, po.received_at as ReceivedAt,
                   po.expected_delivery_date::timestamp as ExpectedDeliveryDate,
                   s.id as SupplierId, s.name as Name, s.email as Email, s.phone as Phone,
                   s.contact_person as ContactPerson
            FROM purchase_orders po
            LEFT JOIN suppliers s ON po.supplier_id = s.id
            WHERE po.id = @Id";

        var orderDict = new Dictionary<int, PurchaseOrder>();
        
        await db.QueryAsync<PurchaseOrder, Supplier, PurchaseOrder>(orderSql,
            (order, supplier) =>
            {
                order.Status = Enum.Parse<PurchaseOrderStatus>(order.Status.ToString(), ignoreCase: true);
                order.Supplier = supplier;
                orderDict[order.Id] = order;
                return order;
            },
            new { Id = id },
            splitOn: "SupplierId");

        if (!orderDict.TryGetValue(id, out var purchaseOrder))
            return null;

        // Load items
        const string itemsSql = @"
            SELECT id as Id, purchase_order_id as PurchaseOrderId, product_id as ProductId,
                   product_name as ProductName, product_sku as ProductSku,
                   quantity as Quantity, unit_cost as UnitCost
            FROM purchase_order_items WHERE purchase_order_id = @OrderId";

        purchaseOrder.Items = (await db.QueryAsync<PurchaseOrderItem>(itemsSql, new { OrderId = id })).ToList();
        
        return purchaseOrder;
    }

    /// <inheritdoc />
    public async Task<PurchaseOrder?> GetByOrderNumberAsync(string orderNumber)
    {
        using var db = CreateConnection();

        const string sql = "SELECT id FROM purchase_orders WHERE order_number = @OrderNumber";
        var id = await db.ExecuteScalarAsync<int?>(sql, new { OrderNumber = orderNumber });

        return id.HasValue ? await GetByIdAsync(id.Value) : null;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PurchaseOrder>> GetAllAsync(PurchaseOrderStatus? statusFilter = null)
    {
        using var db = CreateConnection();

        var sql = @"
            SELECT po.id as Id, po.order_number as OrderNumber, po.supplier_id as SupplierId,
                   po.status::text as StatusText, po.total_amount as TotalAmount, po.notes as Notes,
                   po.created_at as CreatedAt, po.approved_at as ApprovedAt, 
                   po.sent_at as SentAt, po.received_at as ReceivedAt,
                   po.expected_delivery_date::timestamp as ExpectedDeliveryDate,
                   COALESCE(poi_counts.ItemCount, 0) as ItemCount,
                   s.name as SupplierName
            FROM purchase_orders po
            LEFT JOIN (
                SELECT purchase_order_id, COUNT(*)::int as ItemCount
                FROM purchase_order_items
                GROUP BY purchase_order_id
            ) poi_counts ON poi_counts.purchase_order_id = po.id
            LEFT JOIN suppliers s ON po.supplier_id = s.id";

        if (statusFilter.HasValue)
        {
            sql += " WHERE po.status = @Status::purchase_order_status";
        }

        sql += " ORDER BY po.created_at DESC";

        var results = await db.QueryAsync<PurchaseOrderListRow>(sql,
            statusFilter.HasValue ? new { Status = statusFilter.Value.ToString().ToUpper() } : null);

        return results.Select(r =>
        {
            // Defensive: some environments may have legacy rows with NULL supplier_id.
            // Don't crash the UI; show them as unassigned.
            var supplierId = r.SupplierId ?? 0;
            return new PurchaseOrder
            {
                Id = r.Id,
                OrderNumber = r.OrderNumber ?? string.Empty,
                SupplierId = supplierId,
                Status = Enum.TryParse<PurchaseOrderStatus>(r.StatusText ?? "PENDING", true, out var st) ? st : PurchaseOrderStatus.Pending,
                TotalAmount = r.TotalAmount,
                Notes = r.Notes,
                CreatedAt = r.CreatedAt,
                ApprovedAt = r.ApprovedAt,
                SentAt = r.SentAt,
                ReceivedAt = r.ReceivedAt,
                ExpectedDeliveryDate = r.ExpectedDeliveryDate,
                ItemCount = r.ItemCount,
                Supplier = supplierId > 0
                    ? new Supplier { Id = supplierId, Name = r.SupplierName ?? string.Empty, Email = string.Empty }
                    : null
            };
        }).ToList();
    }

    private sealed class PurchaseOrderListRow
    {
        public int Id { get; init; }
        public string? OrderNumber { get; init; }
        public int? SupplierId { get; init; }
        public string? StatusText { get; init; }
        public decimal TotalAmount { get; init; }
        public string? Notes { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ApprovedAt { get; init; }
        public DateTime? SentAt { get; init; }
        public DateTime? ReceivedAt { get; init; }
        public DateTime? ExpectedDeliveryDate { get; init; }
        public int ItemCount { get; init; }
        public string? SupplierName { get; init; }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PurchaseOrder>> GetPendingAsync()
    {
        return await GetAllAsync(PurchaseOrderStatus.Pending);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PurchaseOrder>> GetBySupplierAsync(int supplierId)
    {
        using var db = CreateConnection();

        const string sql = @"
            SELECT id as Id, order_number as OrderNumber, supplier_id as SupplierId,
                   status::text as StatusText, total_amount as TotalAmount,
                   created_at as CreatedAt, approved_at as ApprovedAt
            FROM purchase_orders 
            WHERE supplier_id = @SupplierId
            ORDER BY created_at DESC";

        var results = await db.QueryAsync<dynamic>(sql, new { SupplierId = supplierId });

        return results.Select(r => new PurchaseOrder
        {
            Id = r.Id,
            OrderNumber = r.OrderNumber,
            SupplierId = r.SupplierId,
            Status = Enum.Parse<PurchaseOrderStatus>(r.StatusText, ignoreCase: true),
            TotalAmount = r.TotalAmount,
            CreatedAt = r.CreatedAt,
            ApprovedAt = r.ApprovedAt
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> UpdateStatusAsync(int orderId, PurchaseOrderStatus status, int? approvedByUserId = null)
    {
        using var db = CreateConnection();

        var sql = status == PurchaseOrderStatus.Approved
            ? @"UPDATE purchase_orders 
                SET status = @Status::purchase_order_status, 
                    approved_at = CURRENT_TIMESTAMP,
                    approved_by_user_id = @ApprovedByUserId
                WHERE id = @OrderId"
            : @"UPDATE purchase_orders 
                SET status = @Status::purchase_order_status 
                WHERE id = @OrderId";

        var rowsAffected = await db.ExecuteAsync(sql, new
        {
            OrderId = orderId,
            Status = status.ToString().ToUpper(),
            ApprovedByUserId = approvedByUserId
        });

        if (rowsAffected > 0)
        {
            _logger.LogInformation("Updated PO {OrderId} status to {Status}", orderId, status);
        }

        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> MarkAsSentAsync(int orderId)
    {
        using var db = CreateConnection();

        const string sql = @"
            UPDATE purchase_orders 
            SET status = 'SENT'::purchase_order_status, sent_at = CURRENT_TIMESTAMP
            WHERE id = @OrderId AND status = 'APPROVED'::purchase_order_status";

        var rowsAffected = await db.ExecuteAsync(sql, new { OrderId = orderId });

        if (rowsAffected > 0)
        {
            _logger.LogInformation("Marked PO {OrderId} as sent", orderId);
        }

        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> MarkAsReceivedAsync(int orderId, IEnumerable<(int ProductId, int Quantity)> items)
    {
        using var db = CreateConnection();
        await db.OpenAsync();
        using var transaction = await db.BeginTransactionAsync();

        try
        {
            // Update purchase order status
            const string orderSql = @"
                UPDATE purchase_orders 
                SET status = 'RECEIVED'::purchase_order_status, received_at = CURRENT_TIMESTAMP
                WHERE id = @OrderId AND status = 'SENT'::purchase_order_status";

            var rowsAffected = await db.ExecuteAsync(orderSql, new { OrderId = orderId }, transaction);

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync();
                return false;
            }

            // Update product stock levels for each item atomically
            const string stockSql = @"
                UPDATE products 
                SET current_stock = current_stock + @Quantity,
                    last_updated = CURRENT_TIMESTAMP
                WHERE id = @ProductId";

            foreach (var (productId, quantity) in items)
            {
                await db.ExecuteAsync(stockSql, new { ProductId = productId, Quantity = quantity }, transaction);
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Marked PO {OrderId} as received and updated stock for {ItemCount} products", 
                orderId, items.Count());
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GenerateOrderNumberAsync()
    {
        using var db = CreateConnection();

        const string sql = "SELECT nextval('po_number_seq')";
        var sequence = await db.ExecuteScalarAsync<long>(sql);

        return $"PO-{DateTime.UtcNow:yyyy}-{sequence:D5}";
    }

    public async Task<HashSet<int>> GetOpenProductIdsForSupplierAsync(int supplierId)
    {
        using var db = CreateConnection();

        const string sql = @"
            SELECT DISTINCT poi.product_id
            FROM purchase_order_items poi
            JOIN purchase_orders po ON po.id = poi.purchase_order_id
            WHERE po.supplier_id = @SupplierId
              AND po.status IN (
                'PENDING'::purchase_order_status,
                'APPROVED'::purchase_order_status,
                'SENT'::purchase_order_status
              )";

        var ids = await db.QueryAsync<int>(sql, new { SupplierId = supplierId });
        return ids.ToHashSet();
    }
}
