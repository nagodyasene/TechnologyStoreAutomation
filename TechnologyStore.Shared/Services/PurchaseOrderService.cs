using Microsoft.Extensions.Logging;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Config;

namespace TechnologyStore.Shared.Services;

/// <summary>
/// Business logic service for purchase order management
/// </summary>
public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IProductRepository _productRepository;
    private readonly IEmailService _emailService;
    private readonly BusinessRuleSettings _businessRules;
    private readonly ILogger<PurchaseOrderService> _logger;

    private const string OrderNotFoundMessage = "Purchase order not found.";

    public PurchaseOrderService(
        IPurchaseOrderRepository purchaseOrderRepository,
        ISupplierRepository supplierRepository,
        IProductRepository productRepository,
        IEmailService emailService,
        BusinessRuleSettings businessRules)
    {
        _purchaseOrderRepository = purchaseOrderRepository ?? throw new ArgumentNullException(nameof(purchaseOrderRepository));
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _businessRules = businessRules ?? throw new ArgumentNullException(nameof(businessRules));
        _logger = AppLogger.CreateLogger<PurchaseOrderService>();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PurchaseOrder>> GeneratePurchaseOrdersForLowStockAsync()
    {
        var generatedOrders = new List<PurchaseOrder>();

        try
        {
            // Get dashboard data which includes RunwayDays calculation
            var dashboardData = await _productRepository.GetDashboardDataAsync();

            // Filter products with low stock (RunwayDays <= ReorderRunwayDays) that are ACTIVE
            var lowStockProducts = dashboardData
                .Where(p => p.RunwayDays <= _businessRules.ReorderRunwayDays && p.Phase == "ACTIVE")
                .ToList();

            if (!lowStockProducts.Any())
            {
                _logger.LogInformation("No low-stock products found requiring purchase orders");
                return generatedOrders;
            }

            _logger.LogInformation("Found {Count} low-stock products requiring reorder", lowStockProducts.Count);

            // Get full product details to access SupplierId
            var products = await _productRepository.GetAllAsync();
            var productDict = products.ToDictionary(p => p.Id);

            // Group by supplier
            var productsBySupplier = lowStockProducts
                .Where(p => productDict.ContainsKey(p.Id) && productDict[p.Id].SupplierId.HasValue)
                .GroupBy(p => productDict[p.Id].SupplierId!.Value)
                .ToList();

            foreach (var supplierGroup in productsBySupplier)
            {
                var supplier = await _supplierRepository.GetByIdAsync(supplierGroup.Key);
                if (supplier == null || !supplier.IsActive)
                {
                    _logger.LogWarning("Supplier {SupplierId} not found or inactive, skipping", supplierGroup.Key);
                    continue;
                }

                // Calculate reorder quantities based on runway days
                var items = new List<PurchaseOrderItem>();
                foreach (var dashboardProduct in supplierGroup)
                {
                    var product = productDict[dashboardProduct.Id];

                    // Calculate quantity to order: enough for ReorderRunwayDays worth of stock
                    // Based on SalesLast7Days, estimate daily sales
                    var dailySales = dashboardProduct.SalesLast7Days / 7.0;
                    var targetStock = (int)Math.Ceiling(dailySales * _businessRules.AdequateRunwayDays);
                    var orderQuantity = Math.Max(10, targetStock - dashboardProduct.CurrentStock);

                    items.Add(new PurchaseOrderItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        ProductSku = product.Sku,
                        Quantity = orderQuantity,
                        UnitCost = product.UnitPrice * 0.6m // Assume 40% margin (cost = 60% of retail)
                    });
                }

                // Create the purchase order
                var orderNumber = await _purchaseOrderRepository.GenerateOrderNumberAsync();
                var order = new PurchaseOrder
                {
                    OrderNumber = orderNumber,
                    SupplierId = supplier.Id,
                    Supplier = supplier,
                    Status = PurchaseOrderStatus.Pending,
                    TotalAmount = items.Sum(i => i.Quantity * i.UnitCost),
                    Items = items,
                    Notes = $"Auto-generated due to low stock levels. Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.",
                    ExpectedDeliveryDate = DateTime.UtcNow.AddDays(supplier.LeadTimeDays)
                };

                var createdOrder = await _purchaseOrderRepository.CreateAsync(order);
                generatedOrders.Add(createdOrder);

                _logger.LogInformation("Generated PO {OrderNumber} for supplier {SupplierName} with {ItemCount} items, total ${Total:F2}",
                    orderNumber, supplier.Name, items.Count, order.TotalAmount);
            }

            // Log products without suppliers
            var productsWithoutSupplier = lowStockProducts
                .Where(p => !productDict.ContainsKey(p.Id) || !productDict[p.Id].SupplierId.HasValue)
                .ToList();

            if (productsWithoutSupplier.Any())
            {
                _logger.LogWarning("{Count} low-stock products have no assigned supplier: {Products}",
                    productsWithoutSupplier.Count,
                    string.Join(", ", productsWithoutSupplier.Select(p => p.Name)));
            }
        }
        catch (Exception ex)
        {
            // Don't log and rethrow (S2139) - let caller handle
            throw new InvalidOperationException("Error generating purchase orders for low-stock products", ex);
        }

        return generatedOrders;
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> CreateManualPurchaseOrderAsync(
        int supplierId,
        List<(int ProductId, int Quantity, decimal UnitCost)> items,
        string? notes = null)
    {
        try
        {
            var supplier = await _supplierRepository.GetByIdAsync(supplierId);
            if (supplier == null)
                return PurchaseOrderResult.Failed("Supplier not found.");

            if (!supplier.IsActive)
                return PurchaseOrderResult.Failed("Supplier is not active.");

            if (!items.Any())
                return PurchaseOrderResult.Failed("At least one item is required.");

            var orderItems = new List<PurchaseOrderItem>();
            foreach (var (productId, quantity, unitCost) in items)
            {
                var product = (await _productRepository.GetAllAsync()).FirstOrDefault(p => p.Id == productId);
                if (product == null)
                    return PurchaseOrderResult.Failed($"Product ID {productId} not found.");

                orderItems.Add(new PurchaseOrderItem
                {
                    ProductId = productId,
                    ProductName = product.Name,
                    ProductSku = product.Sku,
                    Quantity = quantity,
                    UnitCost = unitCost
                });
            }

            var orderNumber = await _purchaseOrderRepository.GenerateOrderNumberAsync();
            var order = new PurchaseOrder
            {
                OrderNumber = orderNumber,
                SupplierId = supplierId,
                Supplier = supplier,
                Status = PurchaseOrderStatus.Pending,
                TotalAmount = orderItems.Sum(i => i.Quantity * i.UnitCost),
                Items = orderItems,
                Notes = notes,
                ExpectedDeliveryDate = DateTime.UtcNow.AddDays(supplier.LeadTimeDays)
            };

            var createdOrder = await _purchaseOrderRepository.CreateAsync(order);
            _logger.LogInformation("Created manual PO {OrderNumber} for supplier {SupplierName}", orderNumber, supplier.Name);

            return PurchaseOrderResult.Succeeded(createdOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating manual purchase order");
            return PurchaseOrderResult.Failed("An error occurred while creating the purchase order.");
        }
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> ApproveAsync(int orderId, int approvedByUserId)
    {
        try
        {
            var order = await _purchaseOrderRepository.GetByIdAsync(orderId);
            if (order == null)
                return PurchaseOrderResult.Failed(OrderNotFoundMessage);

            if (order.Status != PurchaseOrderStatus.Pending)
                return PurchaseOrderResult.Failed($"Cannot approve order in '{order.Status}' status.");

            var success = await _purchaseOrderRepository.UpdateStatusAsync(orderId, PurchaseOrderStatus.Approved, approvedByUserId);
            if (!success)
                return PurchaseOrderResult.Failed("Failed to update order status.");

            order.Status = PurchaseOrderStatus.Approved;
            order.ApprovedAt = DateTime.UtcNow;
            order.ApprovedByUserId = approvedByUserId;

            _logger.LogInformation("Approved PO {OrderNumber} by user {UserId}", order.OrderNumber, approvedByUserId);
            return PurchaseOrderResult.Succeeded(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving purchase order {OrderId}", orderId);
            return PurchaseOrderResult.Failed("An error occurred while approving the purchase order.");
        }
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> SendToSupplierAsync(int orderId)
    {
        try
        {
            var order = await _purchaseOrderRepository.GetByIdAsync(orderId);
            if (order == null)
                return PurchaseOrderResult.Failed(OrderNotFoundMessage);

            if (order.Status != PurchaseOrderStatus.Approved)
                return PurchaseOrderResult.Failed($"Cannot send order in '{order.Status}' status. Order must be approved first.");

            var supplier = await _supplierRepository.GetByIdAsync(order.SupplierId);
            if (supplier == null)
                return PurchaseOrderResult.Failed("Supplier not found.");

            // Generate email content
            var emailHtml = GeneratePurchaseOrderEmailHtml(order, supplier);
            var subject = $"Purchase Order {order.OrderNumber} from TechTrend Store";

            // Send email
            var emailSent = await _emailService.SendEmailAsync(supplier.Email, subject, emailHtml);
            if (!emailSent)
            {
                _logger.LogWarning("Failed to send PO email to {Email}", supplier.Email);
                return PurchaseOrderResult.Failed("Failed to send email to supplier.");
            }

            // Update status
            var success = await _purchaseOrderRepository.MarkAsSentAsync(orderId);
            if (!success)
                return PurchaseOrderResult.Failed("Email sent but failed to update order status.");

            order.Status = PurchaseOrderStatus.Sent;
            order.SentAt = DateTime.UtcNow;

            _logger.LogInformation("Sent PO {OrderNumber} to supplier {Email}", order.OrderNumber, supplier.Email);
            return PurchaseOrderResult.Succeeded(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending purchase order {OrderId} to supplier", orderId);
            return PurchaseOrderResult.Failed("An error occurred while sending to supplier.");
        }
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> MarkAsReceivedAsync(int orderId)
    {
        try
        {
            var order = await _purchaseOrderRepository.GetByIdAsync(orderId);
            if (order == null)
                return PurchaseOrderResult.Failed(OrderNotFoundMessage);

            if (order.Status != PurchaseOrderStatus.Sent)
                return PurchaseOrderResult.Failed($"Cannot mark as received. Order is in '{order.Status}' status.");

            // TODO: Update product stock levels when order is received
            // This would involve incrementing CurrentStock for each item in the order

            var success = await _purchaseOrderRepository.MarkAsReceivedAsync(orderId);
            if (!success)
                return PurchaseOrderResult.Failed("Failed to update order status.");

            order.Status = PurchaseOrderStatus.Received;
            order.ReceivedAt = DateTime.UtcNow;

            _logger.LogInformation("Marked PO {OrderNumber} as received", order.OrderNumber);
            return PurchaseOrderResult.Succeeded(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking purchase order {OrderId} as received", orderId);
            return PurchaseOrderResult.Failed("An error occurred.");
        }
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> CancelAsync(int orderId)
    {
        try
        {
            var order = await _purchaseOrderRepository.GetByIdAsync(orderId);
            if (order == null)
                return PurchaseOrderResult.Failed(OrderNotFoundMessage);

            if (order.Status == PurchaseOrderStatus.Sent || order.Status == PurchaseOrderStatus.Received)
                return PurchaseOrderResult.Failed($"Cannot cancel order that has been '{order.Status}'.");

            var success = await _purchaseOrderRepository.UpdateStatusAsync(orderId, PurchaseOrderStatus.Cancelled);
            if (!success)
                return PurchaseOrderResult.Failed("Failed to cancel order.");

            order.Status = PurchaseOrderStatus.Cancelled;
            _logger.LogInformation("Cancelled PO {OrderNumber}", order.OrderNumber);
            return PurchaseOrderResult.Succeeded(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling purchase order {OrderId}", orderId);
            return PurchaseOrderResult.Failed("An error occurred while cancelling the order.");
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PurchaseOrder>> GetAllAsync(PurchaseOrderStatus? statusFilter = null)
    {
        return await _purchaseOrderRepository.GetAllAsync(statusFilter);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrder?> GetByIdAsync(int orderId)
    {
        return await _purchaseOrderRepository.GetByIdAsync(orderId);
    }

    /// <inheritdoc />
    public async Task<int> GetPendingCountAsync()
    {
        var pending = await _purchaseOrderRepository.GetPendingAsync();
        return pending.Count();
    }

    /// <summary>
    /// Generates an HTML email for the purchase order
    /// </summary>
    private static string GeneratePurchaseOrderEmailHtml(PurchaseOrder order, Supplier supplier)
    {
        var itemRows = string.Join("\n", order.Items.Select(item => $@"
            <tr>
                <td style='padding: 8px; border: 1px solid #ddd;'>{item.ProductSku}</td>
                <td style='padding: 8px; border: 1px solid #ddd;'>{item.ProductName}</td>
                <td style='padding: 8px; border: 1px solid #ddd; text-align: center;'>{item.Quantity}</td>
                <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>${item.UnitCost:F2}</td>
                <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>${item.LineTotal:F2}</td>
            </tr>"));

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 800px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2c3e50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; }}
        table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        th {{ background-color: #3498db; color: white; padding: 12px; text-align: left; }}
        .total {{ font-size: 1.2em; font-weight: bold; text-align: right; margin-top: 20px; }}
        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 0.9em; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Purchase Order</h1>
            <h2>{order.OrderNumber}</h2>
        </div>
        <div class='content'>
            <p><strong>Date:</strong> {DateTime.UtcNow:MMMM dd, yyyy}</p>
            <p><strong>To:</strong> {supplier.Name}</p>
            {(string.IsNullOrEmpty(supplier.ContactPerson) ? "" : $"<p><strong>Attention:</strong> {supplier.ContactPerson}</p>")}
            {(order.ExpectedDeliveryDate.HasValue ? $"<p><strong>Expected Delivery:</strong> {order.ExpectedDeliveryDate:MMMM dd, yyyy}</p>" : "")}
            
            <table>
                <thead>
                    <tr>
                        <th>SKU</th>
                        <th>Product</th>
                        <th style='text-align: center;'>Qty</th>
                        <th style='text-align: right;'>Unit Cost</th>
                        <th style='text-align: right;'>Total</th>
                    </tr>
                </thead>
                <tbody>
                    {itemRows}
                </tbody>
            </table>
            
            <div class='total'>
                Total: ${order.TotalAmount:F2}
            </div>
            
            {(string.IsNullOrEmpty(order.Notes) ? "" : $"<p><strong>Notes:</strong> {order.Notes}</p>")}
            
            <div class='footer'>
                <p>This purchase order was generated by TechTrend Automation Dashboard.</p>
                <p>Please confirm receipt of this order by replying to this email.</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }
}
