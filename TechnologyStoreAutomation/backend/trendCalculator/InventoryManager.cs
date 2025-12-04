namespace TechnologyStoreAutomation.backend.trendCalculator;

public class InventoryManager
{
    public string GetRecommendation(InventoryProduct product)
    {
        // 1. Determine Phase
        product.CurrentPhase = CalculatePhase(product);

        // 2. Generate Action
        switch (product.CurrentPhase)
        {
            case LifecyclePhase.Active:
                int reorderAmt = CalculateReorderPoint(product.DailyVelocity);
                return $"STATUS: ACTIVE. Reorder up to {reorderAmt} units.";

            case LifecyclePhase.Legacy:
                return "STATUS: LEGACY. Cap stock at 30 days supply. Apply 15% Discount.";

            case LifecyclePhase.Obsolete:
                return "STATUS: OBSOLETE. DO NOT REORDER. Liquidate immediately.";

            default:
                return "Unknown Status";
        }
    }

    private LifecyclePhase CalculatePhase(InventoryProduct p)
    {
        // Rule 1: Safety Check
        if (p.SupportEndDate.HasValue && p.SupportEndDate.Value < DateTime.Now)
        {
            return LifecyclePhase.Obsolete;
        }

        // Rule 2: Legacy Check
        if (p.SuccessorAnnounced)
        {
            return LifecyclePhase.Legacy;
        }

        // Rule 3: Corporate Blackout Windows (Hardcoded as discussed)
        if (IsBlackoutPeriod(p.Name))
        {
            // During a blackout, we treat Active items effectively as Legacy 
            // to stop aggressive ordering
            return LifecyclePhase.Legacy;
        }

        return LifecyclePhase.Active;
    }

    private int CalculateReorderPoint(double velocity)
    {
        // Standard Deviation logic would go here
        // Simple version: 60 days of stock
        return (int)(velocity * 60);
    }

    private bool IsBlackoutPeriod(string productName)
    {
        var today = DateTime.Now;

        // Example: Apple Blackout (Aug 1 - Sept 20)
        if (productName.Contains("iPhone"))
        {
            if (today.Month == 8 || (today.Month == 9 && today.Day < 20))
                return true;
        }

        return false;
    }
}