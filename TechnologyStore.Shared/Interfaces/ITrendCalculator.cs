using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Interface for trend calculation operations
/// </summary>
public interface ITrendCalculator
{
    /// <summary>
    /// Analyzes sales trends for a product based on its history
    /// </summary>
    /// <param name="product">The product to analyze</param>
    /// <param name="salesHistory">Recent sales transactions (can be null)</param>
    /// <returns>Trend analysis data</returns>
    TrendAnalysis AnalyzeProduct(Product product, IEnumerable<SalesTransaction>? salesHistory);
}
