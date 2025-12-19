using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Config;
using CustomerModel = TechnologyStore.Shared.Models.Customer;

namespace TechnologyStore.Customer.Services;

/// <summary>
/// Generates HTML invoices for orders
/// </summary>
public class InvoiceGenerator
{
    private readonly StoreSettings _storeSettings;

    public InvoiceGenerator(StoreSettings storeSettings)
    {
        _storeSettings = storeSettings ?? throw new ArgumentNullException(nameof(storeSettings));
    }

    /// <summary>
    /// Generates an HTML invoice for the given order
    /// </summary>
    public string GenerateInvoice(Order order, CustomerModel customer)
    {
        var itemRows = string.Join("\n", order.Items.Select(item => $@"
            <tr>
                <td style=""padding: 12px; border-bottom: 1px solid #eee;"">{item.ProductName}</td>
                <td style=""padding: 12px; border-bottom: 1px solid #eee; text-align: center;"">{item.Quantity}</td>
                <td style=""padding: 12px; border-bottom: 1px solid #eee; text-align: right;"">${item.UnitPrice:N2}</td>
                <td style=""padding: 12px; border-bottom: 1px solid #eee; text-align: right;"">${item.LineTotal:N2}</td>
            </tr>"));

        var pickupInfo = order.PickupDate.HasValue
            ? $"Preferred Pickup Date: <strong>{order.PickupDate.Value:MMMM dd, yyyy}</strong>"
            : "Pickup as soon as possible";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <title>Invoice - {order.OrderNumber}</title>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 0; padding: 0; background: #f5f5f5; }}
        .container {{ max-width: 700px; margin: 20px auto; background: white; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #0078D4, #0056a3); color: white; padding: 30px; border-radius: 8px 8px 0 0; }}
        .header h1 {{ margin: 0; font-size: 28px; }}
        .header p {{ margin: 5px 0 0; opacity: 0.9; }}
        .content {{ padding: 30px; }}
        .info-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 30px; }}
        .info-box {{ background: #f8f9fa; padding: 15px; border-radius: 6px; }}
        .info-box h3 {{ margin: 0 0 10px; color: #333; font-size: 14px; text-transform: uppercase; letter-spacing: 0.5px; }}
        .info-box p {{ margin: 5px 0; color: #555; }}
        table {{ width: 100%; border-collapse: collapse; margin-bottom: 20px; }}
        th {{ background: #f8f9fa; padding: 12px; text-align: left; font-weight: 600; }}
        .totals {{ background: #f8f9fa; padding: 20px; border-radius: 6px; }}
        .totals-row {{ display: flex; justify-content: space-between; padding: 8px 0; }}
        .totals-row.total {{ font-size: 18px; font-weight: bold; border-top: 2px solid #ddd; padding-top: 15px; margin-top: 10px; }}
        .pickup-box {{ background: #e7f3ff; border: 1px solid #0078D4; border-radius: 6px; padding: 20px; margin-top: 20px; }}
        .pickup-box h3 {{ color: #0078D4; margin: 0 0 15px; }}
        .footer {{ text-align: center; padding: 20px; color: #888; font-size: 12px; border-top: 1px solid #eee; }}
        .status-badge {{ display: inline-block; background: #ffc107; color: #333; padding: 5px 15px; border-radius: 20px; font-weight: 600; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🧾 Invoice</h1>
            <p>{_storeSettings.Name}</p>
        </div>
        
        <div class=""content"">
            <div class=""info-grid"">
                <div class=""info-box"">
                    <h3>Order Details</h3>
                    <p><strong>Order #:</strong> {order.OrderNumber}</p>
                    <p><strong>Date:</strong> {order.CreatedAt:MMMM dd, yyyy 'at' h:mm tt}</p>
                    <p><strong>Status:</strong> <span class=""status-badge"">{order.Status}</span></p>
                </div>
                <div class=""info-box"">
                    <h3>Customer</h3>
                    <p><strong>{customer.FullName}</strong></p>
                    <p>{customer.Email}</p>
                    {(string.IsNullOrEmpty(customer.Phone) ? "" : $"<p>{customer.Phone}</p>")}
                </div>
            </div>

            <table>
                <thead>
                    <tr>
                        <th>Product</th>
                        <th style=""text-align: center;"">Qty</th>
                        <th style=""text-align: right;"">Unit Price</th>
                        <th style=""text-align: right;"">Total</th>
                    </tr>
                </thead>
                <tbody>
                    {itemRows}
                </tbody>
            </table>

            <div class=""totals"">
                <div class=""totals-row"">
                    <span>Subtotal</span>
                    <span>${order.Subtotal:N2}</span>
                </div>
                <div class=""totals-row"">
                    <span>Tax ({_storeSettings.TaxRate:P0})</span>
                    <span>${order.Tax:N2}</span>
                </div>
                <div class=""totals-row total"">
                    <span>Total Due</span>
                    <span>${order.Total:N2}</span>
                </div>
            </div>

            <div class=""pickup-box"">
                <h3>📍 Pickup Information</h3>
                <p><strong>{_storeSettings.Name}</strong></p>
                <p>{_storeSettings.Address}</p>
                <p>📞 {_storeSettings.Phone}</p>
                <p>🕐 {_storeSettings.OpeningHours}</p>
                <p style=""margin-top: 15px;"">{pickupInfo}</p>
                <p style=""margin-top: 10px; padding: 10px; background: #fff3cd; border-radius: 4px;"">
                    💵 <strong>Payment:</strong> Please pay ${order.Total:N2} in cash when you pick up your order.
                </p>
            </div>

            {(string.IsNullOrEmpty(order.Notes) ? "" : $@"
            <div style=""margin-top: 20px; padding: 15px; background: #f8f9fa; border-radius: 6px;"">
                <strong>Order Notes:</strong>
                <p style=""margin: 10px 0 0;"">{order.Notes}</p>
            </div>")}
        </div>

        <div class=""footer"">
            <p>Thank you for shopping with {_storeSettings.Name}!</p>
            <p>Questions? Contact us at {_storeSettings.Email}</p>
        </div>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// Generates the email subject for an order invoice
    /// </summary>
    public string GenerateSubject(Order order)
    {
        return $"Your Order Confirmation - {order.OrderNumber} | {_storeSettings.Name}";
    }
}
