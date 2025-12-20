using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Result of a purchase order operation
/// </summary>
public class PurchaseOrderResult
{
    public bool Success { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public string? ErrorMessage { get; set; }
    
    public static PurchaseOrderResult Succeeded(PurchaseOrder order) => new() { Success = true, PurchaseOrder = order };
    public static PurchaseOrderResult Failed(string message) => new() { Success = false, ErrorMessage = message };
}

/// <summary>
/// Interface for purchase order business logic
/// </summary>
public interface IPurchaseOrderService
{
    /// <summary>
    /// Scans all products for low stock (RunwayDays <= ReorderRunwayDays) 
    /// and generates purchase orders for products with assigned suppliers
    /// </summary>
    /// <returns>List of generated purchase orders</returns>
    Task<IEnumerable<PurchaseOrder>> GeneratePurchaseOrdersForLowStockAsync();
    
    /// <summary>
    /// Creates a manual purchase order for specific products
    /// </summary>
    Task<PurchaseOrderResult> CreateManualPurchaseOrderAsync(int supplierId, List<(int ProductId, int Quantity, decimal UnitCost)> items, string? notes = null);
    
    /// <summary>
    /// Approves a pending purchase order
    /// </summary>
    /// <param name="orderId">Purchase order ID</param>
    /// <param name="approvedByUserId">User ID of the approving admin</param>
    Task<PurchaseOrderResult> ApproveAsync(int orderId, int approvedByUserId);
    
    /// <summary>
    /// Sends an approved purchase order to the supplier via email
    /// </summary>
    Task<PurchaseOrderResult> SendToSupplierAsync(int orderId);
    
    /// <summary>
    /// Marks an order as received and updates product stock levels
    /// </summary>
    Task<PurchaseOrderResult> MarkAsReceivedAsync(int orderId);
    
    /// <summary>
    /// Cancels a purchase order (only if not yet sent)
    /// </summary>
    Task<PurchaseOrderResult> CancelAsync(int orderId);
    
    /// <summary>
    /// Gets all purchase orders with optional status filter
    /// </summary>
    Task<IEnumerable<PurchaseOrder>> GetAllAsync(PurchaseOrderStatus? statusFilter = null);
    
    /// <summary>
    /// Gets a purchase order by ID
    /// </summary>
    Task<PurchaseOrder?> GetByIdAsync(int orderId);
    
    /// <summary>
    /// Gets count of pending orders awaiting approval
    /// </summary>
    Task<int> GetPendingCountAsync();
}
