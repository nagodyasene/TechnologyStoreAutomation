using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Interface for generating product recommendations
/// </summary>
public interface IRecommendationEngine
{
    /// <summary>
    /// Generates a recommendation for a product based on trend analysis and lifecycle phase
    /// </summary>
    /// <param name="analysis">Trend analysis data</param>
    /// <param name="lifecyclePhase">Current lifecycle phase (ACTIVE, LEGACY, OBSOLETE)</param>
    /// <returns>Recommendation string</returns>
    string GenerateRecommendation(TrendAnalysis analysis, string lifecyclePhase);
}
