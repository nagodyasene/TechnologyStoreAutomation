using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Interfaces;

namespace TechnologyStore.Shared.Services;

/// <summary>
/// Generates actionable recommendations based on trend analysis.
/// Implements IRecommendationEngine for dependency injection and testing.
/// </summary>
public class RecommendationEngine : IRecommendationEngine
{
    #region Constants for Business Rules
    
    // Stock level thresholds
    private const int CriticalRunwayDays = 3;
    private const int UrgentRunwayDays = 7;
    private const int ReorderRunwayDays = 14;
    private const int AdequateRunwayDays = 30;
    
    // Trend strength thresholds
    private const double StrongTrendThreshold = 0.3;
    
    #endregion

    /// <summary>
    /// Generates a human-readable recommendation based on trend analysis and lifecycle phase
    /// </summary>
    public string GenerateRecommendation(TrendAnalysis analysis, string lifecyclePhase)
    {
        // Priority 1: Lifecycle phase overrides
        var lifecycleRecommendation = GetLifecycleRecommendation(analysis, lifecyclePhase);
        if (lifecycleRecommendation != null) 
            return lifecycleRecommendation;

        // Priority 2: Critical stock alerts
        var stockRecommendation = GetStockLevelRecommendation(analysis);
        if (stockRecommendation != null) 
            return stockRecommendation;

        // Priority 3: Trend-based recommendations
        var trendRecommendation = GetTrendRecommendation(analysis);
        if (trendRecommendation != null) 
            return trendRecommendation;

        // Default: All good
        return analysis.RunwayDays > AdequateRunwayDays 
            ? "✅ Normal - Stock adequate" 
            : "✅ Normal";
    }

    /// <summary>
    /// Gets recommendation based on lifecycle phase
    /// </summary>
    private static string? GetLifecycleRecommendation(TrendAnalysis analysis, string lifecyclePhase)
    {
        if (lifecyclePhase == "OBSOLETE")
        {
            return analysis.CurrentStock > 5
                ? "🔴 LIQUIDATE - Clear remaining stock"
                : "🔴 OBSOLETE - Discontinue";
        }

        if (lifecyclePhase == "LEGACY")
        {
            return analysis.RunwayDays < 30
                ? "🟡 LEGACY - Discount 15% to clear"
                : "🟡 LEGACY - Monitor, reduce orders";
        }

        return null;
    }

    /// <summary>
    /// Gets recommendation based on stock runway
    /// </summary>
    private static string? GetStockLevelRecommendation(TrendAnalysis analysis)
    {
        if (analysis.RunwayDays <= CriticalRunwayDays)
            return "🚨 CRITICAL - Reorder IMMEDIATELY";

        if (analysis.RunwayDays <= UrgentRunwayDays)
            return "⚠️ URGENT - Reorder today";

        if (analysis.RunwayDays <= ReorderRunwayDays)
            return "📦 Reorder recommended";

        return null;
    }

    /// <summary>
    /// Gets recommendation based on trend analysis
    /// </summary>
    private static string? GetTrendRecommendation(TrendAnalysis analysis)
    {
        return analysis.Direction switch
        {
            TrendDirection.Rising => GetRisingTrendRecommendation(analysis),
            TrendDirection.Falling => GetFallingTrendRecommendation(analysis),
            TrendDirection.Volatile => "⚡ VOLATILE - Review pricing/promotion",
            _ => null
        };
    }

    /// <summary>
    /// Gets recommendation for rising trend
    /// </summary>
    private static string GetRisingTrendRecommendation(TrendAnalysis analysis)
    {
        if (analysis.IsAccelerating)
            return "🚀 ACCELERATING - Increase stock levels";
        
        if (analysis.TrendStrength > StrongTrendThreshold)
            return "📈 TRENDING UP - Monitor for restock";
        
        return "✅ Normal - Slight increase";
    }

    /// <summary>
    /// Gets recommendation for falling trend
    /// </summary>
    private static string GetFallingTrendRecommendation(TrendAnalysis analysis)
    {
        if (analysis.TrendStrength < -StrongTrendThreshold)
            return "📉 DECLINING - Reduce orders";
        
        return "⚠️ Slight decline - Watch closely";
    }
}
