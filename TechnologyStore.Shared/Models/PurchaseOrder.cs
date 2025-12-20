using System.ComponentModel.DataAnnotations;

namespace TechnologyStore.Shared.Models;

/// <summary>
/// Purchase order status enum
/// </summary>
public enum PurchaseOrderStatus
{
    Pending,
    Approved,
    Sent,
    Received,
    Cancelled
}

/// <summary>
/// Represents a purchase order sent to a supplier
/// </summary>
public class PurchaseOrder
{
    public int Id { get; set; }
    
    /// <summary>
    /// Unique order number (e.g., "PO-2024-00001")
    /// </summary>
    [Required]
    [StringLength(50)]
    public required string OrderNumber { get; set; }
    
    public int SupplierId { get; set; }
    
    /// <summary>
    /// Navigation property for the supplier
    /// </summary>
    public Supplier? Supplier { get; set; }
    
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Pending;
    
    /// <summary>
    /// Total value of the purchase order
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Optional notes for the supplier
    /// </summary>
    [StringLength(1000)]
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ApprovedAt { get; set; }
    
    public int? ApprovedByUserId { get; set; }
    
    public DateTime? SentAt { get; set; }
    
    public DateTime? ReceivedAt { get; set; }
    
    /// <summary>
    /// Expected delivery date based on supplier lead time
    /// </summary>
    public DateTime? ExpectedDeliveryDate { get; set; }
    
    /// <summary>
    /// Line items in this purchase order
    /// </summary>
    public List<PurchaseOrderItem> Items { get; set; } = new();
}

/// <summary>
/// Represents a single line item in a purchase order
/// </summary>
public class PurchaseOrderItem
{
    public int Id { get; set; }
    
    public int PurchaseOrderId { get; set; }
    
    public int ProductId { get; set; }
    
    /// <summary>
    /// Product name snapshot at time of order (in case product name changes)
    /// </summary>
    [StringLength(200)]
    public string? ProductName { get; set; }
    
    /// <summary>
    /// Product SKU snapshot
    /// </summary>
    [StringLength(100)]
    public string? ProductSku { get; set; }
    
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int Quantity { get; set; }
    
    /// <summary>
    /// Unit cost from supplier (may differ from selling price)
    /// </summary>
    [Range(0.01, 999999.99)]
    public decimal UnitCost { get; set; }
    
    /// <summary>
    /// Total cost for this line item
    /// </summary>
    public decimal LineTotal => Quantity * UnitCost;
}
