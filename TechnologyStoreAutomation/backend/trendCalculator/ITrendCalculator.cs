using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation.backend.trendCalculator;

/// <summary>
/// Interface for trend calculator - enables dependency injection and unit testing
/// </summary>
public interface ITrendCalculator
{
    /// <summary>
    /// Analyzes a product's sales history and generates trend insights
    /// </summary>
    TrendAnalysis AnalyzeProduct(Product product, IEnumerable<SalesTransaction>? salesHistory);

    /// <summary>
    /// Quick method to calculate just the runway days without full analysis
    /// </summary>
    int CalculateRunwayDays(int currentStock, double dailySalesAverage);
}

