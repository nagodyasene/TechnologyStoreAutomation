using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Interfaces;

namespace TechnologyStore.Customer.Services;

/// <summary>
/// In-memory shopping cart service for the current session
/// </summary>
public class ShoppingCartService
{
    private readonly List<CartItem> _items = new();
    private readonly IProductRepository _productRepository;
    private decimal _taxRate = 0.10m; // 10% default tax rate

    public ShoppingCartService(IProductRepository productRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    /// <summary>
    /// Sets the tax rate for calculations
    /// </summary>
    public void SetTaxRate(decimal rate)
    {
        _taxRate = rate;
    }

    /// <summary>
    /// Adds a product to the cart or updates quantity if already exists
    /// </summary>
    public async Task<bool> AddItemAsync(int productId, int quantity = 1)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null) return false;

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
        
        if (existingItem != null)
        {
            var newQuantity = existingItem.Quantity + quantity;
            if (newQuantity > product.CurrentStock)
            {
                return false; // Not enough stock
            }
            existingItem.Quantity = newQuantity;
            existingItem.AvailableStock = product.CurrentStock;
        }
        else
        {
            if (quantity > product.CurrentStock)
            {
                return false; // Not enough stock
            }
            
            _items.Add(new CartItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductSku = product.Sku,
                UnitPrice = product.UnitPrice,
                Quantity = quantity,
                AvailableStock = product.CurrentStock
            });
        }

        return true;
    }

    /// <summary>
    /// Removes an item from the cart
    /// </summary>
    public void RemoveItem(int productId)
    {
        _items.RemoveAll(i => i.ProductId == productId);
    }

    /// <summary>
    /// Updates the quantity of an item
    /// </summary>
    public bool UpdateQuantity(int productId, int quantity)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null) return false;

        if (quantity <= 0)
        {
            RemoveItem(productId);
            return true;
        }

        if (quantity > item.AvailableStock)
        {
            return false; // Not enough stock
        }

        item.Quantity = quantity;
        return true;
    }

    /// <summary>
    /// Gets all items in the cart
    /// </summary>
    public IReadOnlyList<CartItem> GetItems() => _items.AsReadOnly();

    /// <summary>
    /// Gets the number of items in the cart
    /// </summary>
    public int ItemCount => _items.Sum(i => i.Quantity);

    /// <summary>
    /// Gets the number of unique products in the cart
    /// </summary>
    public int UniqueItemCount => _items.Count;

    /// <summary>
    /// Calculates the subtotal (before tax)
    /// </summary>
    public decimal Subtotal => _items.Sum(i => i.LineTotal);

    /// <summary>
    /// Calculates the tax amount
    /// </summary>
    public decimal Tax => Subtotal * _taxRate;

    /// <summary>
    /// Calculates the total (including tax)
    /// </summary>
    public decimal Total => Subtotal + Tax;

    /// <summary>
    /// Clears all items from the cart
    /// </summary>
    public void Clear()
    {
        _items.Clear();
    }

    /// <summary>
    /// Checks if the cart is empty
    /// </summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>
    /// Refreshes stock levels for all items in cart
    /// </summary>
    public async Task RefreshStockAsync()
    {
        foreach (var item in _items.ToList())
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId);
            if (product == null)
            {
                // Product no longer available
                _items.Remove(item);
            }
            else
            {
                item.AvailableStock = product.CurrentStock;
                if (item.Quantity > product.CurrentStock)
                {
                    item.Quantity = product.CurrentStock;
                }
            }
        }
    }
}
