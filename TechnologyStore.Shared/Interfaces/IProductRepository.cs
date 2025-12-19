using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Interface for product repository operations - enables dependency injection and unit testing
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// Calculates the 'Closing Stock' and 'Total Sold' for a given date based on the Transaction Ledger.
    /// </summary>
    Task GenerateDailySnapshotAsync(DateTime dateToProcess);

    /// <summary>
    /// Updates the lifecycle status of a product (Active -> Legacy -> Obsolete)
    /// </summary>
    Task UpdateProductPhaseAsync(int productId, string newPhase, string reason);

    /// <summary>
    /// Records a new sale and updates product stock
    /// </summary>
    Task<int> RecordSaleAsync(int productId, int quantitySold, decimal totalAmount, DateTime? saleDate = null);

    /// <summary>
    /// Get sales history for a specific product
    /// </summary>
    Task<IEnumerable<SalesTransaction>> GetSalesHistoryAsync(int productId, int days = 30);

    /// <summary>
    /// Get all products with basic info
    /// </summary>
    Task<IEnumerable<Product>> GetAllProductsAsync();

    /// <summary>
    /// Get dashboard data with trend analysis and recommendations
    /// </summary>
    Task<IEnumerable<ProductDashboardDto>> GetDashboardDataAsync();
    
    /// <summary>
    /// Get a product by ID
    /// </summary>
    Task<Product?> GetByIdAsync(int productId);
    
    /// <summary>
    /// Get products available for purchase (ACTIVE and LEGACY only, with stock > 0)
    /// </summary>
    Task<IEnumerable<Product>> GetAvailableProductsAsync();
    
    /// <summary>
    /// Reserve stock for an order (reduces available stock)
    /// </summary>
    Task<bool> ReserveStockAsync(int productId, int quantity);
    
    /// <summary>
    /// Release reserved stock (e.g., on order cancellation)
    /// </summary>
    Task ReleaseStockAsync(int productId, int quantity);
}
