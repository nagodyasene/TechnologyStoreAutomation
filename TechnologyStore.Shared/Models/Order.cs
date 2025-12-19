namespace TechnologyStore.Shared.Models;

/// <summary>
/// Represents a customer order
/// </summary>
public class Order
{
    public int Id { get; set; }
    public required string OrderNumber { get; set; }
    public int CustomerId { get; set; }
    public string Status { get; set; } = "PENDING";
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public DateTime? PickupDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    public List<OrderItem> Items { get; set; } = new();
    public Customer? Customer { get; set; }
}

/// <summary>
/// Represents an item within an order
/// </summary>
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public required string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Order status constants
/// </summary>
public static class OrderStatus
{
    public const string Pending = "PENDING";
    public const string Confirmed = "CONFIRMED";
    public const string ReadyForPickup = "READY_FOR_PICKUP";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
}
