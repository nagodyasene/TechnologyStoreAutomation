using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Interface for purchase order repository operations
/// </summary>
public interface IPurchaseOrderRepository
{
    /// <summary>
    /// Creates a new purchase order with items
    /// </summary>
    Task<PurchaseOrder> CreateAsync(PurchaseOrder order);

    /// <summary>
    /// Gets a purchase order by ID including items
    /// </summary>
    Task<PurchaseOrder?> GetByIdAsync(int id);

    /// <summary>
    /// Gets a purchase order by order number
    /// </summary>
    Task<PurchaseOrder?> GetByOrderNumberAsync(string orderNumber);

    /// <summary>
    /// Gets all purchase orders with optional status filter
    /// </summary>
    Task<IEnumerable<PurchaseOrder>> GetAllAsync(PurchaseOrderStatus? statusFilter = null);

    /// <summary>
    /// Gets all pending purchase orders (waiting for approval)
    /// </summary>
    Task<IEnumerable<PurchaseOrder>> GetPendingAsync();

    /// <summary>
    /// Gets purchase orders for a specific supplier
    /// </summary>
    Task<IEnumerable<PurchaseOrder>> GetBySupplierAsync(int supplierId);

    /// <summary>
    /// Updates purchase order status
    /// </summary>
    Task<bool> UpdateStatusAsync(int orderId, PurchaseOrderStatus status, int? approvedByUserId = null);

    /// <summary>
    /// Marks a purchase order as sent (updates status and sent timestamp)
    /// </summary>
    Task<bool> MarkAsSentAsync(int orderId);

    /// <summary>
    /// Marks a purchase order as received and updates product stock levels atomically
    /// </summary>
    Task<bool> MarkAsReceivedAsync(int orderId, IEnumerable<(int ProductId, int Quantity)> items);

    /// <summary>
    /// Generates a unique purchase order number (e.g., "PO-2024-00001")
    /// </summary>
    Task<string> GenerateOrderNumberAsync();
}
