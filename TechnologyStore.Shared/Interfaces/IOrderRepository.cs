using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Interface for order repository operations
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Creates a new order with items and reserves stock atomically
    /// </summary>
    Task<Order> CreateOrderAsync(Order order, IEnumerable<(int ProductId, int Quantity)> itemsToReserve);
    
    /// <summary>
    /// Gets an order by ID
    /// </summary>
    Task<Order?> GetByIdAsync(int orderId);
    
    /// <summary>
    /// Gets an order by order number
    /// </summary>
    Task<Order?> GetByOrderNumberAsync(string orderNumber);
    
    /// <summary>
    /// Gets all orders for a customer
    /// </summary>
    Task<IEnumerable<Order>> GetByCustomerIdAsync(int customerId);
    
    /// <summary>
    /// Gets all orders (for staff view)
    /// </summary>
    Task<IEnumerable<Order>> GetAllOrdersAsync(string? statusFilter = null);
    
    /// <summary>
    /// Updates order status
    /// </summary>
    Task UpdateStatusAsync(int orderId, string status);
    
    /// <summary>
    /// Cancels an order and restores product stock levels atomically
    /// </summary>
    Task<bool> CancelOrderAndRestoreStockAsync(int orderId, IEnumerable<(int ProductId, int Quantity)> items);
    
    /// <summary>
    /// Generates a unique order number (e.g., ORD-2024-00001)
    /// </summary>
    Task<string> GenerateOrderNumberAsync();
}
