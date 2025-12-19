using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Interface for order business logic
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Places a new order with stock validation
    /// </summary>
    Task<OrderResult> PlaceOrderAsync(int customerId, List<CartItem> cartItems, string? notes, DateTime? pickupDate);
    
    /// <summary>
    /// Cancels an order and restores stock
    /// </summary>
    Task<bool> CancelOrderAsync(int orderId, int customerId);
}

/// <summary>
/// Result of an order operation
/// </summary>
public class OrderResult
{
    public bool Success { get; set; }
    public Order? Order { get; set; }
    public string? ErrorMessage { get; set; }
    
    public static OrderResult Succeeded(Order order) => new() { Success = true, Order = order };
    public static OrderResult Failed(string message) => new() { Success = false, ErrorMessage = message };
}
