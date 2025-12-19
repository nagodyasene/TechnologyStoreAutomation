namespace TechnologyStore.Shared.Models;

/// <summary>
/// Represents an item in the shopping cart
/// </summary>
public class CartItem
{
    public int ProductId { get; set; }
    public required string ProductName { get; set; }
    public string? ProductSku { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int AvailableStock { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}
