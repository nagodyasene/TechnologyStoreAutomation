namespace TechnologyStoreAutomation.backend.trendCalculator;

public static class InventoryManager
{
    // Constants to avoid hardcoded literals and magic numbers
    private const string AppleProductKeyword = "iPhone";
    private const int BlackoutStartMonth = 8; // August
    private const int BlackoutEndMonth = 9;   // September
    private const int BlackoutEndDay = 20;
    private const int ReorderDays = 60;

    private const string StatusActiveFormat = "STATUS: ACTIVE. Reorder up to {0} units.";
    private const string StatusLegacy = "STATUS: LEGACY. Cap stock at 30 days supply. Apply 15% Discount.";
    private const string StatusObsolete = "STATUS: OBSOLETE. DO NOT REORDER. Liquidate immediately.";
    private const string UnknownStatus = "Unknown Status";

    public static string GetRecommendation(InventoryProduct product)
    {
        // 1. Determine Phase
        product.CurrentPhase = CalculatePhase(product);

        // 2. Generate Action
        switch (product.CurrentPhase)
        {
            case LifecyclePhase.Active:
                int reorderAmt = CalculateReorderPoint(product.DailyVelocity);
                return string.Format(StatusActiveFormat, reorderAmt);

            case LifecyclePhase.Legacy:
                return StatusLegacy;

            case LifecyclePhase.Obsolete:
                return StatusObsolete;

            default:
                return UnknownStatus;
        }
    }

    private static LifecyclePhase CalculatePhase(InventoryProduct p)
    {
        // Rule 1: Safety Check
        if (p.SupportEndDate.HasValue && p.SupportEndDate.Value < DateTime.UtcNow)
        {
            return LifecyclePhase.Obsolete;
        }

        // Rule 2: Legacy Check
        if (p.SuccessorAnnounced)
        {
            return LifecyclePhase.Legacy;
        }

        // Rule 3: Corporate Blackout Windows (configured via constants)
        if (IsBlackoutPeriod(p.Name))
        {
            // During a blackout, we treat Active items effectively as Legacy 
            // to stop aggressive ordering
            return LifecyclePhase.Legacy;
        }

        return LifecyclePhase.Active;
    }

    private static int CalculateReorderPoint(double velocity)
    {
        // Standard Deviation logic would go here
        // Simple version: ReorderDays days of stock
        return (int)(velocity * ReorderDays);
    }

    private static bool IsBlackoutPeriod(string productName)
    {
        var today = DateTime.UtcNow;

        // Example: Apple Blackout (Aug 1 - Sept 20)
        if (!string.IsNullOrEmpty(productName) && productName.Contains(AppleProductKeyword, StringComparison.OrdinalIgnoreCase)
            && (today.Month == BlackoutStartMonth || (today.Month == BlackoutEndMonth && today.Day < BlackoutEndDay)))
            return true;
        

        return false;
    }
}