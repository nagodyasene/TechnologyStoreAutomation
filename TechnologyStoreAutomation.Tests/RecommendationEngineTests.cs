using TechnologyStoreAutomation.backend.trendCalculator;

namespace TechnologyStoreAutomation.Tests;

public class RecommendationEngineTests
{
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
        var result = RecommendationEngine.GenerateRecommendation(analysis, "OBSOLETE");
        Assert.True(result.Contains("LIQUIDATE"), $"Expected 'LIQUIDATE' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_ObsoleteNoStock_ReturnsDiscontinue()
    {
        var analysis = CreateAnalysis(currentStock: 3);
        var result = RecommendationEngine.GenerateRecommendation(analysis, "OBSOLETE");
        Assert.True(result.Contains("Discontinue"), $"Expected 'Discontinue' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_LegacyLowRunway_ReturnsDiscount()
    {
        var analysis = CreateAnalysis(runwayDays: 20);
        var result = RecommendationEngine.GenerateRecommendation(analysis, "LEGACY");
        Assert.True(result.Contains("Discount"), $"Expected 'Discount' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_LegacyHighRunway_ReturnsMonitor()
    {
        var analysis = CreateAnalysis(runwayDays: 60);
        var result = RecommendationEngine.GenerateRecommendation(analysis, "LEGACY");
        Assert.True(result.Contains("Monitor"), $"Expected 'Monitor' in '{result}'");
    }

    // Critical Stock Alerts
    [Fact]
    public void GenerateRecommendation_CriticalRunway_ReturnsCriticalAlert()
    {
        var analysis = CreateAnalysis(runwayDays: 2);
        var result = RecommendationEngine.GenerateRecommendation(analysis, "ACTIVE");
        Assert.True(result.Contains("CRITICAL"), $"Expected 'CRITICAL' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_UrgentRunway_ReturnsUrgentAlert()
    {
        var analysis = CreateAnalysis(runwayDays: 5);
        var result = RecommendationEngine.GenerateRecommendation(analysis, "ACTIVE");
        Assert.True(result.Contains("URGENT"), $"Expected 'URGENT' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_LowRunway_ReturnsReorderRecommended()
    {
        var analysis = CreateAnalysis(runwayDays: 10);
        var result = RecommendationEngine.GenerateRecommendation(analysis, "ACTIVE");
        Assert.True(result.Contains("Reorder"), $"Expected 'Reorder' in '{result}'");
    }

    // Trend-Based Recommendations
    [Fact]
    public void GenerateRecommendation_AcceleratingTrend_ReturnsAccelerating()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Rising,
            isAccelerating: true);
        var result = RecommendationEngine.GenerateRecommendation(analysis, "ACTIVE");
        Assert.True(result.Contains("ACCELERATING"), $"Expected 'ACCELERATING' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_StrongRisingTrend_ReturnsTrendingUp()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Rising,
            trendStrength: 0.5);
        var result = RecommendationEngine.GenerateRecommendation(analysis, "ACTIVE");
        Assert.True(result.Contains("TRENDING UP"), $"Expected 'TRENDING UP' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_FallingTrend_ReturnsDeclineWarning()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Falling,
            trendStrength: -0.5);
        var result = RecommendationEngine.GenerateRecommendation(analysis, "ACTIVE");
        Assert.True(result.Contains("DECLINING"), $"Expected 'DECLINING' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_VolatileTrend_ReturnsVolatileWarning()
    {
        var analysis = CreateAnalysis(
            runwayDays: 30,
            direction: TrendDirection.Volatile);
        var result = RecommendationEngine.GenerateRecommendation(analysis, "ACTIVE");
        Assert.True(result.Contains("VOLATILE"), $"Expected 'VOLATILE' in '{result}'");
    }

    [Fact]
    public void GenerateRecommendation_NormalStock_ReturnsNormal()
    {
        var analysis = CreateAnalysis(runwayDays: 45);
        var result = RecommendationEngine.GenerateRecommendation(analysis, "ACTIVE");
        Assert.True(result.Contains("Normal"), $"Expected 'Normal' in '{result}'");
    }

    // Reorder Quantity Tests
    [Fact]
    public void CalculateReorderQuantity_NoSales_ReturnsZero()
    {
        var analysis = CreateAnalysis(dailyAverage: 0);
        var result = RecommendationEngine.CalculateReorderQuantity(analysis);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateReorderQuantity_AdequateStock_ReturnsZeroOrNegative()
    {
        var analysis = CreateAnalysis(currentStock: 200, dailyAverage: 5); // 40 days runway
        var result = RecommendationEngine.CalculateReorderQuantity(analysis, targetRunwayDays: 30);
        Assert.Equal(0, result); // Already above target
    }

    [Fact]
    public void CalculateReorderQuantity_LowStock_ReturnsPositiveQuantity()
    {
        var analysis = CreateAnalysis(currentStock: 50, dailyAverage: 10); // 5 days runway
        var result = RecommendationEngine.CalculateReorderQuantity(analysis, targetRunwayDays: 30);
        Assert.True(result > 0, $"Expected positive quantity, got {result}");
    }

    [Fact]
    public void CalculateReorderQuantity_RisingTrend_IncreasesQuantity()
    {
        var analysisStable = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Stable);
        var analysisRising = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Rising);

        var stableQty = RecommendationEngine.CalculateReorderQuantity(analysisStable);
        var risingQty = RecommendationEngine.CalculateReorderQuantity(analysisRising);

        Assert.True(risingQty > stableQty, $"Expected risingQty ({risingQty}) > stableQty ({stableQty})");
    }

    [Fact]
    public void CalculateReorderQuantity_FallingTrend_DecreasesQuantity()
    {
        var analysisStable = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Stable);
        var analysisFalling = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Falling);

        var stableQty = RecommendationEngine.CalculateReorderQuantity(analysisStable);
        var fallingQty = RecommendationEngine.CalculateReorderQuantity(analysisFalling);

        Assert.True(fallingQty < stableQty, $"Expected fallingQty ({fallingQty}) < stableQty ({stableQty})");
    }

    [Fact]
    public void CalculateReorderQuantity_Accelerating_AddsBuffer()
    {
        var analysisNormal = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Rising);
        var analysisAccel = CreateAnalysis(currentStock: 50, dailyAverage: 10, direction: TrendDirection.Rising, isAccelerating: true);

        var normalQty = RecommendationEngine.CalculateReorderQuantity(analysisNormal);
        var accelQty = RecommendationEngine.CalculateReorderQuantity(analysisAccel);

        Assert.True(accelQty > normalQty, $"Expected accelQty ({accelQty}) > normalQty ({normalQty})");
    }
}

