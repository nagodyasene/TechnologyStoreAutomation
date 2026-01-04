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

    private const string OrderNotFoundMessage = "Satın alma siparişi bulunamadı.";

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
        return await GeneratePurchaseOrdersByRunwayThresholdAsync(
            runwayDaysThreshold: _businessRules.ReorderRunwayDays,
            label: "düşük stok");
    }

    public async Task<IEnumerable<PurchaseOrder>> GeneratePurchaseOrdersForUrgentStockAsync()
    {
        return await GeneratePurchaseOrdersByRunwayThresholdAsync(
            runwayDaysThreshold: _businessRules.UrgentRunwayDays,
            label: "acil stok");
    }

    public async Task<IEnumerable<PurchaseOrder>> GeneratePurchaseOrdersForCriticalStockAsync()
    {
        return await GeneratePurchaseOrdersByRunwayThresholdAsync(
            runwayDaysThreshold: _businessRules.CriticalRunwayDays,
            label: "kritik stok");
    }

    private async Task<IEnumerable<PurchaseOrder>> GeneratePurchaseOrdersByRunwayThresholdAsync(int runwayDaysThreshold, string label)
    {
        var generatedOrders = new List<PurchaseOrder>();

        try
        {
            // Get dashboard data which includes RunwayDays calculation
            var dashboardData = (await _productRepository.GetDashboardDataAsync()).ToList();

            // Filter products that are ACTIVE and below threshold
            var matchingProducts = dashboardData
                .Where(p => p.RunwayDays <= runwayDaysThreshold && p.Phase == "ACTIVE")
                .ToList();

            if (!matchingProducts.Any())
            {
                _logger.LogInformation("No {Label} products found requiring purchase orders", label);
                return generatedOrders;
            }

            _logger.LogInformation("Found {Count} {Label} products requiring reorder", matchingProducts.Count, label);

            // Get full product details to access SupplierId
            var products = await _productRepository.GetAllAsync();
            var productsList = products?.ToList() ?? new List<Product>();

            var productDict = productsList.ToDictionary(p => p.Id);

            // Group by supplier (only those with SupplierId)
            var productsBySupplier = matchingProducts
                .Where(p => productDict.ContainsKey(p.Id) && productDict[p.Id].SupplierId.HasValue)
                .GroupBy(p => productDict[p.Id].SupplierId!.Value)
                .ToList();

            var skippedAlreadyOpen = 0;
            var skippedSupplierMissing = 0;
            var skippedSupplierInactive = 0;
            var createdOrderCount = 0;

            foreach (var supplierGroup in productsBySupplier)
            {
                var supplierId = supplierGroup.Key;
                var supplier = await _supplierRepository.GetByIdAsync(supplierId);
                if (supplier == null || !supplier.IsActive)
                {
                    skippedSupplierInactive += supplierGroup.Count();
                    _logger.LogWarning("Supplier {SupplierId} not found or inactive, skipping", supplierId);
                    continue;
                }

                // Prevent duplicates: skip products already on an open PO for this supplier
                var openProductIds = await _purchaseOrderRepository.GetOpenProductIdsForSupplierAsync(supplierId);

                // Calculate reorder quantities based on runway days
                var items = new List<PurchaseOrderItem>();
                foreach (var dashboardProduct in supplierGroup)
                {
                    var product = productDict[dashboardProduct.Id];
                    if (openProductIds.Contains(product.Id))
                    {
                        skippedAlreadyOpen++;
                        continue;
                    }

                    // Estimate daily sales from last 7 days and order up to AdequateRunwayDays of stock
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

                if (items.Count == 0)
                {
                    continue;
                }

                // Create the purchase order
                var orderNumber = await _purchaseOrderRepository.GenerateOrderNumberAsync();
                var order = new PurchaseOrder
                {
                    OrderNumber = orderNumber,
                    SupplierId = supplier.Id,
                    Supplier = supplier,
                    Status = PurchaseOrderStatus.Pending, // admin must approve
                    TotalAmount = items.Sum(i => i.Quantity * i.UnitCost),
                    Items = items,
                    Notes = $"Otomatik oluşturuldu: {label} (RunwayDays <= {runwayDaysThreshold}). Oluşturma zamanı: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC.",
                    ExpectedDeliveryDate = DateTime.UtcNow.AddDays(supplier.LeadTimeDays)
                };

                var createdOrder = await _purchaseOrderRepository.CreateAsync(order);
                generatedOrders.Add(createdOrder);
                createdOrderCount++;

                _logger.LogInformation("Generated PO {OrderNumber} for supplier {SupplierName} with {ItemCount} items, total ${Total:F2}",
                    orderNumber, supplier.Name, items.Count, order.TotalAmount);
            }

            // Log products without suppliers
            var productsWithoutSupplier = matchingProducts
                .Where(p => !productDict.ContainsKey(p.Id) || !productDict[p.Id].SupplierId.HasValue)
                .ToList();

            if (productsWithoutSupplier.Any())
            {
                skippedSupplierMissing = productsWithoutSupplier.Count;
                _logger.LogWarning("{Count} {Label} products have no assigned supplier: {Products}",
                    productsWithoutSupplier.Count,
                    label,
                    string.Join(", ", productsWithoutSupplier.Select(p => p.Name)));
            }
        }
        catch (Exception ex)
        {
            // Don't log and rethrow (S2139) - let caller handle
            throw new InvalidOperationException($"{label} ürünler için satın alma siparişleri oluşturulurken hata oluştu", ex);
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
                return PurchaseOrderResult.Failed("Tedarikçi bulunamadı.");

            if (!supplier.IsActive)
                return PurchaseOrderResult.Failed("Tedarikçi aktif değil.");

            if (!items.Any())
                return PurchaseOrderResult.Failed("En az bir kalem gerekli.");

            var orderItems = new List<PurchaseOrderItem>();
            var allProducts = await _productRepository.GetAllAsync();
            if (allProducts == null)
                return PurchaseOrderResult.Failed("Ürün listesi alınamadı.");
                
            foreach (var (productId, quantity, unitCost) in items)
            {
                var product = allProducts.FirstOrDefault(p => p.Id == productId);
                if (product == null)
                    return PurchaseOrderResult.Failed($"Ürün bulunamadı (ID: {productId}).");

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
            return PurchaseOrderResult.Failed("Satın alma siparişi oluşturulurken bir hata oluştu.");
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
                return PurchaseOrderResult.Failed($"'{order.Status}' durumundaki sipariş onaylanamaz.");

            var success = await _purchaseOrderRepository.UpdateStatusAsync(orderId, PurchaseOrderStatus.Approved, approvedByUserId);
            if (!success)
                return PurchaseOrderResult.Failed("Sipariş durumu güncellenemedi.");

            order.Status = PurchaseOrderStatus.Approved;
            order.ApprovedAt = DateTime.UtcNow;
            order.ApprovedByUserId = approvedByUserId;

            _logger.LogInformation("Approved PO {OrderNumber} by user {UserId}", order.OrderNumber, approvedByUserId);
            return PurchaseOrderResult.Succeeded(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving purchase order {OrderId}", orderId);
            return PurchaseOrderResult.Failed("Satın alma siparişi onaylanırken bir hata oluştu.");
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
                return PurchaseOrderResult.Failed($"'{order.Status}' durumundaki sipariş gönderilemez. Önce onaylanmalıdır.");

            var supplier = await _supplierRepository.GetByIdAsync(order.SupplierId);
            if (supplier == null)
                return PurchaseOrderResult.Failed("Tedarikçi bulunamadı.");

            var emailConfigured = await _emailService.IsConfiguredAsync();
            
            if (!emailConfigured)
            {
                return PurchaseOrderResult.Failed(
                    "E-posta servisi yapılandırılmamış.\n\n" +
                    "Gönderimi etkinleştirmek için:\n" +
                    "- Gmail OAuth kimlik bilgilerini 'credentials.json' olarak uygulama çalışma dizinine koyun, VEYA\n" +
                    "- Araçlar → Ayarlar içinden E-posta Test Modu'nu açın (göndermek yerine loglar).");
            }

            // Generate email content
            var emailHtml = GeneratePurchaseOrderEmailHtml(order, supplier);
            var subject = $"Satın Alma Siparişi {order.OrderNumber} - TechTrend Store";

            // Send email
            var emailSent = await _emailService.SendEmailAsync(supplier.Email, subject, emailHtml);
            if (!emailSent)
            {
                _logger.LogWarning("Failed to send PO email to {Email}", supplier.Email);
                if (_emailService is TechnologyStore.Shared.Interfaces.IEmailServiceDiagnostics diag &&
                    !string.IsNullOrWhiteSpace(diag.LastErrorMessage))
                {
                    return PurchaseOrderResult.Failed(diag.LastErrorMessage);
                }

                return PurchaseOrderResult.Failed("E-posta tedarikçiye gönderilemedi.");
            }

            // Update status
            var success = await _purchaseOrderRepository.MarkAsSentAsync(orderId);
            if (!success)
                return PurchaseOrderResult.Failed("E-posta gönderildi ancak sipariş durumu güncellenemedi.");

            order.Status = PurchaseOrderStatus.Sent;
            order.SentAt = DateTime.UtcNow;

            _logger.LogInformation("Sent PO {OrderNumber} to supplier {Email}", order.OrderNumber, supplier.Email);
            return PurchaseOrderResult.Succeeded(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending purchase order {OrderId} to supplier", orderId);
            return PurchaseOrderResult.Failed("Tedarikçiye gönderim sırasında bir hata oluştu.");
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
                return PurchaseOrderResult.Failed($"Teslim alındı olarak işaretlenemez. Sipariş durumu: '{order.Status}'.");

            // Update order status and product stock levels atomically
            var items = order.Items.Select(item => (item.ProductId, item.Quantity)).ToList();
            var success = await _purchaseOrderRepository.MarkAsReceivedAsync(orderId, items);
            if (!success)
                return PurchaseOrderResult.Failed("Sipariş durumu güncellenemedi. Sipariş zaten teslim alınmış olabilir veya geçersiz durumda olabilir.");

            // Invalidate dashboard/product caches (Desktop uses a cached decorator)
            if (_productRepository is TechnologyStore.Shared.Interfaces.IProductCacheInvalidation cacheInvalidation)
            {
                cacheInvalidation.InvalidateProductCaches();
            }

            _logger.LogDebug("Updated stock for {ItemCount} products from PO {OrderNumber}",
                items.Count, order.OrderNumber);

            order.Status = PurchaseOrderStatus.Received;
            order.ReceivedAt = DateTime.UtcNow;

            _logger.LogInformation("Marked PO {OrderNumber} as received, updated stock for {ItemCount} products",
                order.OrderNumber, order.Items.Count);
            return PurchaseOrderResult.Succeeded(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking purchase order {OrderId} as received", orderId);
            return PurchaseOrderResult.Failed("Bir hata oluştu.");
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
                return PurchaseOrderResult.Failed($"'{order.Status}' durumundaki sipariş iptal edilemez.");

            var success = await _purchaseOrderRepository.UpdateStatusAsync(orderId, PurchaseOrderStatus.Cancelled);
            if (!success)
                return PurchaseOrderResult.Failed("Sipariş iptal edilemedi.");

            order.Status = PurchaseOrderStatus.Cancelled;
            _logger.LogInformation("Cancelled PO {OrderNumber}", order.OrderNumber);
            return PurchaseOrderResult.Succeeded(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling purchase order {OrderId}", orderId);
            return PurchaseOrderResult.Failed("Sipariş iptal edilirken bir hata oluştu.");
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
            <h1>Satın Alma Siparişi</h1>
            <h2>{order.OrderNumber}</h2>
        </div>
        <div class='content'>
            <p><strong>Tarih:</strong> {DateTime.UtcNow:dd.MM.yyyy}</p>
            <p><strong>Alıcı:</strong> {supplier.Name}</p>
            {(string.IsNullOrEmpty(supplier.ContactPerson) ? "" : $"<p><strong>İlgili:</strong> {supplier.ContactPerson}</p>")}
            {(order.ExpectedDeliveryDate.HasValue ? $"<p><strong>Beklenen Teslim:</strong> {order.ExpectedDeliveryDate:dd.MM.yyyy}</p>" : "")}
            
            <table>
                <thead>
                    <tr>
                        <th>SKU</th>
                        <th>Ürün</th>
                        <th style='text-align: center;'>Adet</th>
                        <th style='text-align: right;'>Birim Maliyet</th>
                        <th style='text-align: right;'>Toplam</th>
                    </tr>
                </thead>
                <tbody>
                    {itemRows}
                </tbody>
            </table>
            
            <div class='total'>
                Toplam: ${order.TotalAmount:F2}
            </div>
            
            {(string.IsNullOrEmpty(order.Notes) ? "" : $"<p><strong>Notlar:</strong> {order.Notes}</p>")}
            
            <div class='footer'>
                <p>Bu satın alma siparişi TechTrend Otomasyon Paneli tarafından oluşturulmuştur.</p>
                <p>Lütfen bu e-postaya yanıt vererek siparişi aldığınızı teyit edin.</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }
}
