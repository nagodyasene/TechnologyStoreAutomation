namespace TechnologyStoreAutomation.backend.trendCalculator.data;

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
}

