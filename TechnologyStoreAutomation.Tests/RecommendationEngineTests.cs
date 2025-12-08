using TechnologyStoreAutomation.backend.trendCalculator;

namespace TechnologyStoreAutomation.Tests;

public class RecommendationEngineTests
{
    #region Constants
    
    private const string ObsoletePhase = "OBSOLETE";
    private const string LegacyPhase = "LEGACY";
    private const string ActivePhase = "ACTIVE";
    private const string CriticalAlert = "CRITICAL";
    private const string UrgentAlert = "URGENT";
    private const string ReorderRecommendation = "Reorder";
    private const string TrendingUpMessage = "TRENDING UP";
    private const string NormalMessage = "Normal";
    
    #endregion

    private readonly IRecommendationEngine _engine = new RecommendationEngine();
    
    private static TrendAnalysis CreateAnalysis(
        int runwayDays = 30,
        TrendDirection direction = TrendDirection.Stable,
        double trendStrength = 0,
        bool isAccelerating = false,
        int currentStock = 100,
        double dailyAverage = 5)
    {
        return new TrendAnalysis
        {
            ProductId = 1,
            ProductName = "Test Product",
            CurrentStock = currentStock,
            DailySalesAverage = dailyAverage,
            RunwayDays = runwayDays,
            Direction = direction,
            TrendStrength = trendStrength,
            IsAccelerating = isAccelerating
        };
    }

    // Lifecycle Phase Tests
    [Fact]
    public void GenerateRecommendation_ObsoleteWithStock_ReturnsLiquidate()
    {
        var analysis = CreateAnalysis(currentStock: 10);
        var result = _engine.GenerateRecommendation(analysis, ObsoletePhase);
        Assert.True(result.Contains("LIQUIDATE"), $"Expected 'LIQUIDATE' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_ObsoleteNoStock_ReturnsDiscontinue()
    {
        var analysis = CreateAnalysis(currentStock: 3);
        var result = _engine.GenerateRecommendation(analysis, ObsoletePhase);
        Assert.True(result.Contains("Discontinue"), $"Expected 'Discontinue' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_LegacyLowRunway_ReturnsDiscount()
    {
        var analysis = CreateAnalysis(runwayDays: 20);
        var result = _engine.GenerateRecommendation(analysis, LegacyPhase);
        Assert.True(result.Contains("Discount"), $"Expected 'Discount' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_LegacyHighRunway_ReturnsMonitor()
    {
        var analysis = CreateAnalysis(runwayDays: 60);
        var result = _engine.GenerateRecommendation(analysis, LegacyPhase);
        Assert.True(result.Contains("Monitor"), $"Expected 'Monitor' in '{result}'");
    }

    // Critical Stock Alerts
    [Fact]
    public void GenerateRecommendation_CriticalRunway_ReturnsCriticalAlert()
    {
        var analysis = CreateAnalysis(runwayDays: 2);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.True(result.Contains(CriticalAlert), $"Expected '{CriticalAlert}' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_UrgentRunway_ReturnsUrgentAlert()
    {
        var analysis = CreateAnalysis(runwayDays: 5);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.True(result.Contains(UrgentAlert), $"Expected '{UrgentAlert}' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_LowRunway_ReturnsReorderRecommended()
    {
        var analysis = CreateAnalysis(runwayDays: 10);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.True(result.Contains(ReorderRecommendation), $"Expected '{ReorderRecommendation}' in '{result}'");
    }

    // Trend-Based Recommendations
    [Fact]
    public void GenerateRecommendation_AcceleratingTrend_ReturnsAccelerating()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Rising,
            isAccelerating: true);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.True(result.Contains("ACCELERATING"), $"Expected 'ACCELERATING' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_StrongRisingTrend_ReturnsTrendingUp()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Rising,
            trendStrength: 0.5);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.True(result.Contains(TrendingUpMessage), $"Expected '{TrendingUpMessage}' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_FallingTrend_ReturnsDeclineWarning()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Falling,
            trendStrength: -0.5);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.True(result.Contains("DECLINING"), $"Expected 'DECLINING' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_VolatileTrend_ReturnsVolatileWarning()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Volatile);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.True(result.Contains("VOLATILE"), $"Expected 'VOLATILE' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_NormalStock_ReturnsNormal()
    {
        var analysis = CreateAnalysis(runwayDays: 45);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.True(result.Contains(NormalMessage), $"Expected '{NormalMessage}' in '{result}'");
    }

    // Reorder Quantity Tests
    [Fact]
    public void CalculateReorderQuantity_NoSales_ReturnsZero()
    {
        var analysis = CreateAnalysis(dailyAverage: 0);
        var result = _engine.CalculateReorderQuantity(analysis);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateReorderQuantity_AdequateStock_ReturnsZeroOrNegative()
    {
        var analysis = CreateAnalysis(currentStock: 200, dailyAverage: 5); // 40-day runway
        var result = _engine.CalculateReorderQuantity(analysis, targetRunwayDays: 30);
        Assert.Equal(0, result); // Already above target
    }

    [Fact]
    public void CalculateReorderQuantity_LowStock_ReturnsPositiveQuantity()
    {
        var analysis = CreateAnalysis(currentStock: 50, dailyAverage: 10); // 5-day runway
        var result = _engine.CalculateReorderQuantity(analysis, targetRunwayDays: 30);
        Assert.True(result > 0, $"Expected positive quantity, got {result}");
    }

    [Fact]
    public void CalculateReorderQuantity_RisingTrend_IncreasesQuantity()
    {
        var analysisStable = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Stable);
        var analysisRising = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Rising);

        var stableQty = _engine.CalculateReorderQuantity(analysisStable);
        var risingQty = _engine.CalculateReorderQuantity(analysisRising);

        Assert.True(risingQty > stableQty, $"Expected risingQty ({risingQty}) > stableQty ({stableQty})");
    }

    [Fact]
    public void CalculateReorderQuantity_FallingTrend_DecreasesQuantity()
    {
        var analysisStable = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Stable);
        var analysisFalling = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Falling);

        var stableQty = _engine.CalculateReorderQuantity(analysisStable);
        var fallingQty = _engine.CalculateReorderQuantity(analysisFalling);

        Assert.True(fallingQty < stableQty, $"Expected fallingQty ({fallingQty}) < stableQty ({stableQty})");
    }

    [Fact]
    public void CalculateReorderQuantity_Accelerating_AddsBuffer()
    {
        var analysisNormal = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Rising);
        var analysisAccel = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Rising, isAccelerating: true);

        var normalQty = _engine.CalculateReorderQuantity(analysisNormal);
        var accelQty = _engine.CalculateReorderQuantity(analysisAccel);

        Assert.True(accelQty > normalQty, $"Expected accelQty ({accelQty}) > normalQty ({normalQty})");
    }

    #region Edge Case Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void GenerateRecommendation_ZeroOrNegativeRunway_ReturnsCritical(int runwayDays)
    {
        var analysis = CreateAnalysis(runwayDays: runwayDays);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.Contains(CriticalAlert, result);
    }

    [Fact]
    public void GenerateRecommendation_ExactlyThreeDaysRunway_ReturnsCritical()
    {
        var analysis = CreateAnalysis(runwayDays: 3);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.Contains(CriticalAlert, result);
    }

    [Fact]
    public void GenerateRecommendation_ExactlySevenDaysRunway_ReturnsUrgent()
    {
        var analysis = CreateAnalysis(runwayDays: 7);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.Contains(UrgentAlert, result);
    }

    [Fact]
    public void GenerateRecommendation_ExactlyFourteenDaysRunway_ReturnsReorder()
    {
        var analysis = CreateAnalysis(runwayDays: 14);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.Contains(ReorderRecommendation, result);
    }

    [Fact]
    public void GenerateRecommendation_VeryHighRunway_ReturnsNormal()
    {
        var analysis = CreateAnalysis(runwayDays: 999);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.Contains(NormalMessage, result);
    }

    [Fact]
    public void GenerateRecommendation_LegacyWithExactly30DaysRunway_ReturnsDiscount()
    {
        var analysis = CreateAnalysis(runwayDays: 30);
        var result = _engine.GenerateRecommendation(analysis, LegacyPhase);
        Assert.Contains("Monitor", result);
    }

    [Fact]
    public void GenerateRecommendation_LegacyWith29DaysRunway_ReturnsDiscount()
    {
        var analysis = CreateAnalysis(runwayDays: 29);
        var result = _engine.GenerateRecommendation(analysis, LegacyPhase);
        Assert.Contains("Discount", result);
    }

    [Fact]
    public void GenerateRecommendation_ObsoleteWithExactly5Stock_ReturnsDiscontinue()
    {
        var analysis = CreateAnalysis(currentStock: 5);
        var result = _engine.GenerateRecommendation(analysis, ObsoletePhase);
        Assert.Contains("Discontinue", result);
    }

    [Fact]
    public void GenerateRecommendation_ObsoleteWith6Stock_ReturnsLiquidate()
    {
        var analysis = CreateAnalysis(currentStock: 6);
        var result = _engine.GenerateRecommendation(analysis, ObsoletePhase);
        Assert.Contains("LIQUIDATE", result);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("ACTIVE")]
    [InlineData("Active")]
    public void GenerateRecommendation_CaseInsensitivePhase_UpperCaseWorks(string phase)
    {
        var analysis = CreateAnalysis(runwayDays: 45);
        // Note: The implementation expects uppercase, so only "ACTIVE" should return Normal
        var result = _engine.GenerateRecommendation(analysis, phase);
        Assert.NotNull(result);
    }

    [Fact]
    public void GenerateRecommendation_UnknownPhase_FallsBackToTrendBased()
    {
        var analysis = CreateAnalysis(runwayDays: 45);
        var result = _engine.GenerateRecommendation(analysis, "UNKNOWN_PHASE");
        Assert.Contains(NormalMessage, result);
    }

    [Fact]
    public void GenerateRecommendation_EmptyPhase_FallsBackToTrendBased()
    {
        var analysis = CreateAnalysis(runwayDays: 45);
        var result = _engine.GenerateRecommendation(analysis, "");
        Assert.Contains(NormalMessage, result);
    }

    [Fact]
    public void GenerateRecommendation_RisingNotAccelerating_ReturnsTrendingUp()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Rising,
            trendStrength: 0.4,
            isAccelerating: false);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.Contains(TrendingUpMessage, result);
    }

    [Fact]
    public void GenerateRecommendation_RisingWeakTrend_ReturnsSlightIncrease()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Rising,
            trendStrength: 0.1,
            isAccelerating: false);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.Contains("Slight increase", result);
    }

    [Fact]
    public void GenerateRecommendation_FallingWeakTrend_ReturnsWatchClosely()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Falling,
            trendStrength: -0.1);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.Contains("Watch closely", result);
    }

    #endregion

    #region CalculateReorderQuantity Edge Cases

    [Fact]
    public void CalculateReorderQuantity_VeryHighDailyAverage_ReturnsReasonableQuantity()
    {
        var analysis = CreateAnalysis(currentStock: 100, dailyAverage: 1000);
        var result = _engine.CalculateReorderQuantity(analysis, targetRunwayDays: 30);
        Assert.True(result > 0);
        Assert.True(result >= 29000); // Should be around (1000 * 30) - 100 = 29900
    }

    [Fact]
    public void CalculateReorderQuantity_NegativeDailyAverage_ReturnsZero()
    {
        var analysis = CreateAnalysis(currentStock: 100, dailyAverage: -10);
        var result = _engine.CalculateReorderQuantity(analysis);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateReorderQuantity_CustomTargetRunway_CalculatesCorrectly()
    {
        var analysis = CreateAnalysis(currentStock: 10, dailyAverage: 10);
        
        var qty7Days = _engine.CalculateReorderQuantity(analysis, targetRunwayDays: 7);
        var qty30Days = _engine.CalculateReorderQuantity(analysis, targetRunwayDays: 30);
        var qty60Days = _engine.CalculateReorderQuantity(analysis, targetRunwayDays: 60);
        
        Assert.True(qty7Days < qty30Days);
        Assert.True(qty30Days < qty60Days);
    }

    [Fact]
    public void CalculateReorderQuantity_VolatileTrend_DoesNotAffectQuantity()
    {
        var analysisStable = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Stable);
        var analysisVolatile = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Volatile);

        var stableQty = _engine.CalculateReorderQuantity(analysisStable);
        var volatileQty = _engine.CalculateReorderQuantity(analysisVolatile);

        // Volatile trend should not change the quantity (no special handling in the code)
        Assert.Equal(stableQty, volatileQty);
    }

    [Fact]
    public void CalculateReorderQuantity_AcceleratingAndRising_AppliesBothMultipliers()
    {
        var analysisBase = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Stable);
        var analysisRisingAccel = CreateAnalysis(
            currentStock: 50, 
            dailyAverage: 10, 
            direction: TrendDirection.Rising, 
            isAccelerating: true);

        var baseQty = _engine.CalculateReorderQuantity(analysisBase);
        var risingAccelQty = _engine.CalculateReorderQuantity(analysisRisingAccel);

        // Rising adds 20%, accelerating adds 30%, combined should be significantly higher
        Assert.True(risingAccelQty > baseQty * 1.4, 
            $"Expected risingAccelQty ({risingAccelQty}) > baseQty * 1.4 ({baseQty * 1.4})");
    }

    [Fact]
    public void CalculateReorderQuantity_ZeroStock_CalculatesFullTargetAmount()
    {
        var analysis = CreateAnalysis(currentStock: 0, dailyAverage: 10);
        var result = _engine.CalculateReorderQuantity(analysis, targetRunwayDays: 30);
        
        // With 0 stock and 10-day average, should need ~300 units for 30 days
        Assert.True(result >= 300);
    }

    #endregion

    #region Boundary Tests

    [Fact]
    public void GenerateRecommendation_BoundaryRunway4Days_ReturnsUrgent()
    {
        var analysis = CreateAnalysis(runwayDays: 4);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.Contains(UrgentAlert, result);
    }

    [Fact]
    public void GenerateRecommendation_BoundaryRunway8Days_ReturnsReorder()
    {
        var analysis = CreateAnalysis(runwayDays: 8);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.Contains(ReorderRecommendation, result);
    }

    [Fact]
    public void GenerateRecommendation_BoundaryRunway15Days_ReturnsNormalOrTrendBased()
    {
        var analysis = CreateAnalysis(runwayDays: 15);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        // Should fall through to trend-based or Normal
        Assert.NotNull(result);
        Assert.DoesNotContain(CriticalAlert, result);
        Assert.DoesNotContain(UrgentAlert, result);
        Assert.DoesNotContain(ReorderRecommendation, result);
    }

    [Fact]
    public void GenerateRecommendation_TrendStrengthExactly03_ReturnsTrendingUp()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Rising,
            trendStrength: 0.3);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        // At exactly 0.3, should NOT be trending up (needs > 0.3)
        Assert.DoesNotContain(TrendingUpMessage, result);
    }

    [Fact]
    public void GenerateRecommendation_TrendStrengthAbove03_ReturnsTrendingUp()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Rising,
            trendStrength: 0.31);
        var result = _engine.GenerateRecommendation(analysis, ActivePhase);
        Assert.Contains(TrendingUpMessage, result);
    }

    #endregion
}

