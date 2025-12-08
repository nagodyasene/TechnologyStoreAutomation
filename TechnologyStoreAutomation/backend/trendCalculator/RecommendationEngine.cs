namespace TechnologyStoreAutomation.backend.trendCalculator;

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
    
    // Reorder quantity adjustments
    private const double RisingTrendMultiplier = 1.2;
    private const double FallingTrendMultiplier = 0.8;
    private const double AcceleratingMultiplier = 1.3;
    
    // Lifecycle thresholds
    private const int LegacyAgeDays = 365;
    private const int VeryOldAgeDays = 730;
    private const int ObsoleteAgeDays = 1095;
    private const int ObsoleteNoSalesDays = 90;
    
    // Discount percentages
    private const int ObsoleteBulkDiscount = 40;
    private const int ObsoleteMediumDiscount = 30;
    private const int ObsoleteSmallDiscount = 25;
    private const int LegacySlowMovingDiscount = 20;
    private const int LegacyMediumDiscount = 15;
    private const int LegacyFastMovingDiscount = 10;
    
    #endregion

    /// <summary>
    /// Generates a human-readable recommendation based on trend analysis and lifecycle phase
    /// </summary>
    public string GenerateRecommendation(TrendAnalysis analysis, string lifecyclePhase)
    {
        // Priority 1: Lifecycle phase overrides
        if (lifecyclePhase == "OBSOLETE")
        {
            if (analysis.CurrentStock > 5)
                return "🔴 LIQUIDATE - Clear remaining stock";
            else
                return "🔴 OBSOLETE - Discontinue";
        }

        if (lifecyclePhase == "LEGACY")
        {
            if (analysis.RunwayDays < 30)
                return "🟡 LEGACY - Discount 15% to clear";
            else
                return "🟡 LEGACY - Monitor, reduce orders";
        }

        // Priority 2: Critical stock alerts
        if (analysis.RunwayDays <= CriticalRunwayDays)
            return "🚨 CRITICAL - Reorder IMMEDIATELY";

        if (analysis.RunwayDays <= UrgentRunwayDays)
            return "⚠️ URGENT - Reorder today";

        if (analysis.RunwayDays <= ReorderRunwayDays)
            return "📦 Reorder recommended";

        // Priority 3: Trend-based recommendations
        if (analysis.Direction == TrendDirection.Rising)
        {
            if (analysis.IsAccelerating)
                return "🚀 ACCELERATING - Increase stock levels";
            else if (analysis.TrendStrength > StrongTrendThreshold)
                return "📈 TRENDING UP - Monitor for restock";
            else
                return "✅ Normal - Slight increase";
        }

        if (analysis.Direction == TrendDirection.Falling)
        {
            if (analysis.TrendStrength < -StrongTrendThreshold)
                return "📉 DECLINING - Reduce orders";
            else
                return "⚠️ Slight decline - Watch closely";
        }

        if (analysis.Direction == TrendDirection.Volatile)
        {
            return "⚡ VOLATILE - Review pricing/promotion";
        }

        // Default: All good
        if (analysis.RunwayDays > AdequateRunwayDays)
            return "✅ Normal - Stock adequate";

        return "✅ Normal";
    }

    /// <summary>
    /// Calculates suggested reorder quantity based on trends
    /// </summary>
    public int CalculateReorderQuantity(TrendAnalysis analysis, int targetRunwayDays = 30)
    {
        if (analysis.DailySalesAverage <= 0) return 0;

        // Calculate the quantity needed to reach the target runway
        double targetStock = analysis.DailySalesAverage * targetRunwayDays;
        int reorderQty = (int)Math.Ceiling(targetStock - analysis.CurrentStock);

        // Adjust for the trend direction
        if (analysis.Direction == TrendDirection.Rising)
        {
            reorderQty = (int)(reorderQty * RisingTrendMultiplier);
        }
        else if (analysis.Direction == TrendDirection.Falling)
        {
            reorderQty = (int)(reorderQty * FallingTrendMultiplier);
        }

        // If accelerating, add a buffer
        if (analysis.IsAccelerating)
        {
            reorderQty = (int)(reorderQty * AcceleratingMultiplier);
        }

        return Math.Max(0, reorderQty);
    }

    /// <summary>
    /// Determines if a product should be marked as LEGACY
    /// </summary>
    public bool ShouldMarkAsLegacy(TrendAnalysis analysis, DateTime productCreatedDate, bool hasSuccessor)
    {
        var ageInDays = (DateTime.Today - productCreatedDate).Days;

        // If successor exists and sales declining
        if (hasSuccessor && analysis.Direction == TrendDirection.Falling)
            return true;

        // If the product is old (>1 year) and sales are low
        if (ageInDays > LegacyAgeDays && analysis.DailySalesAverage < 1.0)
            return true;

        // If the product is very old (>2 years) regardless of sales
        if (ageInDays > VeryOldAgeDays)
            return true;

        return false;
    }

    /// <summary>
    /// Determines if a product should be marked as OBSOLETE
    /// </summary>
    public bool ShouldMarkAsObsolete(TrendAnalysis analysis, DateTime productCreatedDate, string currentPhase)
    {
        var ageInDays = (DateTime.Today - productCreatedDate).Days;

        // Must already be LEGACY
        if (currentPhase != "LEGACY") return false;

        // If no sales in the last 30 days and old stock
        if (analysis.DailySalesAverage == 0 && ageInDays > ObsoleteNoSalesDays)
            return true;

        // If extremely old (>3 years) as LEGACY
        if (ageInDays > ObsoleteAgeDays)
            return true;

        return false;
    }

    /// <summary>
    /// Calculates suggested discount percentage for legacy/obsolete products
    /// </summary>
    public int CalculateSuggestedDiscount(string lifecyclePhase, int runwayDays, int currentStock)
    {
        if (lifecyclePhase == "OBSOLETE")
        {
            if (currentStock > 20) return ObsoleteBulkDiscount;
            if (currentStock > 10) return ObsoleteMediumDiscount;
            return ObsoleteSmallDiscount;
        }

        if (lifecyclePhase == "LEGACY")
        {
            if (runwayDays > 60) return LegacySlowMovingDiscount;
            if (runwayDays > 30) return LegacyMediumDiscount;
            return LegacyFastMovingDiscount;
        }

        return 0; // No discount for ACTIVE
    }
    
    #region Static Methods (for backward compatibility)
    
    /// <summary>
    /// Static instance for backward compatibility with existing code
    /// </summary>
    private static readonly RecommendationEngine Instance = new();
    
    /// <summary>
    /// Static method for backward compatibility
    /// </summary>
    public static string GetRecommendation(TrendAnalysis analysis, string lifecyclePhase) 
        => Instance.GenerateRecommendation(analysis, lifecyclePhase);
    
    /// <summary>
    /// Static method for backward compatibility
    /// </summary>
    public static int GetReorderQuantity(TrendAnalysis analysis, int targetRunwayDays = 30) 
        => Instance.CalculateReorderQuantity(analysis, targetRunwayDays);
    
    #endregion
}

