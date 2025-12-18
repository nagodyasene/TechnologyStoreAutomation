namespace TechnologyStore.Desktop.Features.Products;

/// <summary>
/// Interface for recommendation engine - enables dependency injection and unit testing
/// </summary>
public interface IRecommendationEngine
{
    /// <summary>
    /// Generates a human-readable recommendation based on trend analysis and lifecycle phase
    /// </summary>
    string GenerateRecommendation(TrendAnalysis analysis, string lifecyclePhase);

    /// <summary>
    /// Calculates suggested reorder quantity based on trends
    /// </summary>
    int CalculateReorderQuantity(TrendAnalysis analysis, int targetRunwayDays = 30);

    /// <summary>
    /// Determines if a product should be marked as LEGACY
    /// </summary>
    bool ShouldMarkAsLegacy(TrendAnalysis analysis, DateTime productCreatedDate, bool hasSuccessor);

    /// <summary>
    /// Determines if a product should be marked as OBSOLETE
    /// </summary>
    bool ShouldMarkAsObsolete(TrendAnalysis analysis, DateTime productCreatedDate, string currentPhase);

    /// <summary>
    /// Calculates suggested discount percentage for legacy/obsolete products
    /// </summary>
    int CalculateSuggestedDiscount(string lifecyclePhase, int runwayDays, int currentStock);
}

