namespace TechnologyStoreAutomation.backend.trendCalculator;

/// <summary>
/// Generates actionable recommendations based on trend analysis
/// </summary>
public class RecommendationEngine
{
    /// <summary>
    /// Generates a human-readable recommendation based on trend analysis and lifecycle phase
    /// </summary>
    public static string GenerateRecommendation(TrendAnalysis analysis, string lifecyclePhase)
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
        if (analysis.RunwayDays <= 3)
            return "🚨 CRITICAL - Reorder IMMEDIATELY";

        if (analysis.RunwayDays <= 7)
            return "⚠️ URGENT - Reorder today";

        if (analysis.RunwayDays <= 14)
            return "📦 Reorder recommended";

        // Priority 3: Trend-based recommendations
        if (analysis.Direction == TrendDirection.Rising)
        {
            if (analysis.IsAccelerating)
                return "🚀 ACCELERATING - Increase stock levels";
            else if (analysis.TrendStrength > 0.3)
                return "📈 TRENDING UP - Monitor for restock";
            else
                return "✅ Normal - Slight increase";
        }

        if (analysis.Direction == TrendDirection.Falling)
        {
            if (analysis.TrendStrength < -0.3)
                return "📉 DECLINING - Reduce orders";
            else
                return "⚠️ Slight decline - Watch closely";
        }

        if (analysis.Direction == TrendDirection.Volatile)
        {
            return "⚡ VOLATILE - Review pricing/promotion";
        }

        // Default: All good
        if (analysis.RunwayDays > 30)
            return "✅ Normal - Stock adequate";

        return "✅ Normal";
    }

    /// <summary>
    /// Calculates suggested reorder quantity based on trends
    /// </summary>
    public static int CalculateReorderQuantity(TrendAnalysis analysis, int targetRunwayDays = 30)
    {
        if (analysis.DailySalesAverage <= 0) return 0;

        // Calculate quantity needed to reach target runway
        double targetStock = analysis.DailySalesAverage * targetRunwayDays;
        int reorderQty = (int)Math.Ceiling(targetStock - analysis.CurrentStock);

        // Adjust for trend direction
        if (analysis.Direction == TrendDirection.Rising)
        {
            reorderQty = (int)(reorderQty * 1.2); // Order 20% more for rising trend
        }
        else if (analysis.Direction == TrendDirection.Falling)
        {
            reorderQty = (int)(reorderQty * 0.8); // Order 20% less for falling trend
        }

        // If accelerating, add buffer
        if (analysis.IsAccelerating)
        {
            reorderQty = (int)(reorderQty * 1.3);
        }

        return Math.Max(0, reorderQty);
    }

    /// <summary>
    /// Determines if a product should be marked as LEGACY
    /// </summary>
    public static bool ShouldMarkAsLegacy(TrendAnalysis analysis, DateTime productCreatedDate, bool hasSuccessor)
    {
        var ageInDays = (DateTime.Today - productCreatedDate).Days;

        // If successor exists and sales declining
        if (hasSuccessor && analysis.Direction == TrendDirection.Falling)
            return true;

        // If product is old (>1 year) and sales are low
        if (ageInDays > 365 && analysis.DailySalesAverage < 1.0)
            return true;

        // If product is very old (>2 years) regardless of sales
        if (ageInDays > 730)
            return true;

        return false;
    }

    /// <summary>
    /// Determines if a product should be marked as OBSOLETE
    /// </summary>
    public static bool ShouldMarkAsObsolete(TrendAnalysis analysis, DateTime productCreatedDate, string currentPhase)
    {
        var ageInDays = (DateTime.Today - productCreatedDate).Days;

        // Must already be LEGACY
        if (currentPhase != "LEGACY") return false;

        // If no sales in last 30 days and old stock
        if (analysis.DailySalesAverage == 0 && ageInDays > 90)
            return true;

        // If extremely old (>3 years) as LEGACY
        if (ageInDays > 1095)
            return true;

        return false;
    }

    /// <summary>
    /// Calculates suggested discount percentage for legacy/obsolete products
    /// </summary>
    public static int CalculateSuggestedDiscount(string lifecyclePhase, int runwayDays, int currentStock)
    {
        if (lifecyclePhase == "OBSOLETE")
        {
            if (currentStock > 20) return 40; // Heavy discount for bulk obsolete stock
            if (currentStock > 10) return 30;
            return 25;
        }

        if (lifecyclePhase == "LEGACY")
        {
            if (runwayDays > 60) return 20; // Slow moving legacy
            if (runwayDays > 30) return 15;
            return 10;
        }

        return 0; // No discount for ACTIVE
    }
}

