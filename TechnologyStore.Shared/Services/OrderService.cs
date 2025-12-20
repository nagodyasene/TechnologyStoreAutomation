using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace TechnologyStore.Shared.Services;

/// <summary>
/// Business logic service for order operations
/// </summary>
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _logger = AppLogger.CreateLogger<OrderService>();
    }

    public async Task<OrderResult> PlaceOrderAsync(int customerId, List<CartItem> cartItems, string? notes, DateTime? pickupDate)
    {
        if (cartItems == null || cartItems.Count == 0)
        {
            return OrderResult.Failed("Cart is empty.");
        }

        _logger.LogInformation("Placing order for customer {CustomerId} with {ItemCount} items", customerId, cartItems.Count);

        var stockErrors = await ValidateStockAsync(cartItems);
        if (stockErrors.Count > 0)
        {
            return OrderResult.Failed(string.Join("\n", stockErrors));
        }

        var reservedItems = new List<(int ProductId, int Quantity)>();
        try
        {
            var reserveResult = await TryReserveStockAsync(cartItems, reservedItems);
            if (reserveResult != null)
            {
                return reserveResult;
            }

            var orderNumber = await _orderRepository.GenerateOrderNumberAsync();
            var order = CreateOrderFromCart(customerId, cartItems, notes, pickupDate, orderNumber);

            var createdOrder = await _orderRepository.CreateOrderAsync(order);

            _logger.LogInformation("Order {OrderNumber} created successfully for customer {CustomerId}",
                createdOrder.OrderNumber, customerId);

            return OrderResult.Succeeded(createdOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order for customer {CustomerId}", customerId);
            await RollbackReservedStockAsync(reservedItems);
            return OrderResult.Failed("An error occurred while placing your order. Please try again.");
        }
    }

    private async Task<List<string>> ValidateStockAsync(List<CartItem> cartItems)
    {
        var errors = new List<string>();
        foreach (var item in cartItems)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId);
            if (product == null)
            {
                errors.Add($"Product '{item.ProductName}' is no longer available.");
                continue;
            }

            if (product.CurrentStock < item.Quantity)
            {
                var message = product.CurrentStock == 0
                    ? $"'{item.ProductName}' is out of stock."
                    : $"Only {product.CurrentStock} units of '{item.ProductName}' available (requested {item.Quantity}).";
                errors.Add(message);
            }
        }
        return errors;
    }

    private async Task<OrderResult?> TryReserveStockAsync(List<CartItem> cartItems, List<(int ProductId, int Quantity)> reservedItems)
    {
        foreach (var item in cartItems)
        {
            var reserved = await _productRepository.ReserveStockAsync(item.ProductId, item.Quantity);
            if (!reserved)
            {
                await RollbackReservedStockAsync(reservedItems);
                return OrderResult.Failed($"Failed to reserve stock for '{item.ProductName}'. Please try again.");
            }
            reservedItems.Add((item.ProductId, item.Quantity));
        }
        return null;
    }

    private async Task RollbackReservedStockAsync(List<(int ProductId, int Quantity)> reservedItems)
    {
        foreach (var (productId, quantity) in reservedItems)
        {
            try
            {
                await _productRepository.ReleaseStockAsync(productId, quantity);
            }
            catch (Exception releaseEx)
            {
                _logger.LogError(releaseEx, "Failed to release stock for product {ProductId}", productId);
            }
        }
    }

    private static Order CreateOrderFromCart(int customerId, List<CartItem> cartItems, string? notes, DateTime? pickupDate, string orderNumber)
    {
        var subtotal = cartItems.Sum(i => i.LineTotal);
        var tax = subtotal * 0.10m;
        var total = subtotal + tax;

        return new Order
        {
            OrderNumber = orderNumber,
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            Subtotal = subtotal,
            Tax = tax,
            Total = total,
            Notes = notes,
            PickupDate = pickupDate,
            Items = cartItems.Select(c => new OrderItem
            {
                ProductId = c.ProductId,
                ProductName = c.ProductName,
                UnitPrice = c.UnitPrice,
                Quantity = c.Quantity,
                LineTotal = c.LineTotal
            }).ToList()
        };
    }


    public async Task<bool> CancelOrderAsync(int orderId, int customerId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order == null)
        {
            _logger.LogWarning("Cancel failed: Order {OrderId} not found", orderId);
            return false;
        }

        if (order.CustomerId != customerId)
        {
            _logger.LogWarning("Cancel failed: Customer {CustomerId} does not own order {OrderId}", customerId, orderId);
            return false;
        }

        // Only allow cancellation of pending orders
        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
        {
            _logger.LogWarning("Cancel failed: Order {OrderId} cannot be cancelled (status: {Status})", orderId, order.Status);
            return false;
        }

        // Restore stock
        foreach (var item in order.Items)
        {
            await _productRepository.ReleaseStockAsync(item.ProductId, item.Quantity);
        }

        // Update order status
        await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Cancelled);

        _logger.LogInformation("Order {OrderNumber} cancelled, stock restored", order.OrderNumber);
        return true;
    }
}
