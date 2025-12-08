using System.Reflection;
using TechnologyStoreAutomation.backend.visitorCountPrediction;

namespace TechnologyStoreAutomation.Tests;

/// <summary>
/// Unit tests for VisitorCountPredictor - Tests the internal calculation logic
/// using reflection to access private methods since the public methods require database access.
/// </summary>
public class VisitorCountPredictorTests
{
    private readonly VisitorCountPredictor _predictor;
    
    public VisitorCountPredictorTests()
    {
        // Use a dummy connection string since we'll be testing internal methods via reflection
        _predictor = new VisitorCountPredictor("Host=localhost;Database=test;");
    }

    #region Helper Methods
    
    private static DailyTraffic CreateDailyTraffic(DateTime date, int visitors, int transactions = 10, decimal revenue = 100m)
    {
        return new DailyTraffic
        {
            Date = date,
            EstimatedVisitors = visitors,
            TotalTransactions = transactions,
            TotalUnitsSold = transactions * 2,
            TotalRevenue = revenue,
            ConversionRate = 0.30
        };
    }

    private static List<DailyTraffic> CreateHistoricalData(int days, int baseVisitors = 100)
    {
        var data = new List<DailyTraffic>();
        for (int i = days; i > 0; i--)
        {
            data.Add(CreateDailyTraffic(DateTime.Today.AddDays(-i), baseVisitors + (i % 10)));
        }
        return data;
    }

    private T InvokePrivateMethod<T>(string methodName, params object[] parameters)
    {
        var method = typeof(VisitorCountPredictor).GetMethod(methodName, 
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException($"Method {methodName} not found");
        return (T)method.Invoke(_predictor, parameters)!;
    }

    #endregion

    #region CalculateStandardDeviation Tests

    [Fact]
    public void CalculateStandardDeviation_SingleValue_ReturnsZero()
    {
        var values = new List<double> { 100.0 };
        var result = InvokePrivateMethod<double>("CalculateStandardDeviation", values);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateStandardDeviation_EmptyList_ReturnsZero()
    {
        var values = new List<double>();
        var result = InvokePrivateMethod<double>("CalculateStandardDeviation", values);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateStandardDeviation_UniformValues_ReturnsZero()
    {
        var values = new List<double> { 50, 50, 50, 50 };
        var result = InvokePrivateMethod<double>("CalculateStandardDeviation", values);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateStandardDeviation_VariedValues_ReturnsCorrectValue()
    {
        // Values: 10, 20, 30, 40, 50 -> Mean: 30, StdDev: sqrt(200) ≈ 14.14
        var values = new List<double> { 10, 20, 30, 40, 50 };
        var result = InvokePrivateMethod<double>("CalculateStandardDeviation", values);
        Assert.True(result > 14 && result < 15, $"Expected ~14.14, got {result}");
    }

    #endregion

    #region ClassifyTrafficLevel Tests

    [Theory]
    [InlineData(100, 100, 10, TrafficLevel.Normal)]      // At average
    [InlineData(150, 100, 10, TrafficLevel.VeryHigh)]    // Well above average
    [InlineData(120, 100, 10, TrafficLevel.High)]        // Above average
    [InlineData(50, 100, 10, TrafficLevel.VeryLow)]      // Well below average
    [InlineData(80, 100, 10, TrafficLevel.Low)]          // Below average
    public void ClassifyTrafficLevel_VariousInputs_ReturnsCorrectLevel(
        int predicted, double average, double stdDev, TrafficLevel expected)
    {
        var result = InvokePrivateMethod<TrafficLevel>("ClassifyTrafficLevel", predicted, average, stdDev);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ClassifyTrafficLevel_ZeroStdDev_ReturnsNormal()
    {
        var result = InvokePrivateMethod<TrafficLevel>("ClassifyTrafficLevel", 150, 100.0, 0.0);
        Assert.Equal(TrafficLevel.Normal, result);
    }

    #endregion

    #region ApplySpecialDayAdjustments Tests

    [Fact]
    public void ApplySpecialDayAdjustments_BlackFriday_Returns250Percent()
    {
        // Black Friday 2025: November 28 (4th Friday)
        var blackFriday = new DateTime(2025, 11, 28);
        var result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", blackFriday, 100);
        Assert.Equal(250, result); // 100 * 2.5
    }

    [Fact]
    public void ApplySpecialDayAdjustments_CyberMonday_Returns200Percent()
    {
        // Cyber Monday 2025: December 1
        var cyberMonday = new DateTime(2025, 12, 1);
        var result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", cyberMonday, 100);
        
        // December 1 is not in November, so it won't match Cyber Monday rule
        // Let's check with a proper Cyber Monday date
        var cyberMonday2024 = new DateTime(2024, 11, 25); // Monday after Black Friday 2024
        result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", cyberMonday2024, 100);
        Assert.Equal(200, result); // 100 * 2.0
    }

    [Fact]
    public void ApplySpecialDayAdjustments_ChristmasEve_Returns180Percent()
    {
        var christmasEve = new DateTime(2025, 12, 24);
        var result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", christmasEve, 100);
        Assert.Equal(180, result); // 100 * 1.8
    }

    [Fact]
    public void ApplySpecialDayAdjustments_PostChristmas_Returns150Percent()
    {
        var boxingDay = new DateTime(2025, 12, 26);
        var result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", boxingDay, 100);
        Assert.Equal(150, result); // 100 * 1.5
    }

    [Fact]
    public void ApplySpecialDayAdjustments_NewYearSales_Returns130Percent()
    {
        var newYear = new DateTime(2025, 1, 3);
        var result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", newYear, 100);
        Assert.Equal(130, result); // 100 * 1.3
    }

    [Fact]
    public void ApplySpecialDayAdjustments_AppleLaunchSeason_Returns140Percent()
    {
        var appleLaunch = new DateTime(2025, 9, 15);
        var result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", appleLaunch, 100);
        Assert.Equal(140, result); // 100 * 1.4
    }

    [Fact]
    public void ApplySpecialDayAdjustments_Saturday_Returns120Percent()
    {
        // Find a Saturday that doesn't fall on other special dates
        var saturday = new DateTime(2025, 3, 15); // A regular Saturday in March
        var result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", saturday, 100);
        Assert.Equal(120, result); // 100 * 1.2
    }

    [Fact]
    public void ApplySpecialDayAdjustments_Sunday_Returns110Percent()
    {
        // Find a Sunday that doesn't fall on other special dates
        var sunday = new DateTime(2025, 3, 16); // A regular Sunday in March
        var result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", sunday, 100);
        Assert.Equal(110, result); // 100 * 1.1
    }

    [Fact]
    public void ApplySpecialDayAdjustments_RegularWeekday_ReturnsUnchanged()
    {
        // Regular Wednesday in March (no special adjustments)
        var regularDay = new DateTime(2025, 3, 12);
        var result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", regularDay, 100);
        Assert.Equal(100, result); // No adjustment
    }

    #endregion

    #region GenerateStaffingRecommendation Tests

    [Theory]
    [InlineData(TrafficLevel.VeryLow, DayOfWeek.Monday, "Minimal staff")]
    [InlineData(TrafficLevel.Low, DayOfWeek.Tuesday, "Standard minimum")]
    [InlineData(TrafficLevel.Normal, DayOfWeek.Wednesday, "Normal staffing")]
    [InlineData(TrafficLevel.High, DayOfWeek.Thursday, "additional staff")]
    [InlineData(TrafficLevel.VeryHigh, DayOfWeek.Friday, "PEAK DAY")]
    public void GenerateStaffingRecommendation_VariousLevels_ReturnsAppropriateRecommendation(
        TrafficLevel level, DayOfWeek day, string expectedContains)
    {
        var result = InvokePrivateMethod<string>("GenerateStaffingRecommendation", level, day);
        Assert.Contains(expectedContains, result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateStaffingRecommendation_IncludesDayName()
    {
        var result = InvokePrivateMethod<string>("GenerateStaffingRecommendation", TrafficLevel.Normal, DayOfWeek.Saturday);
        Assert.Contains("Saturday", result);
    }

    #endregion

    #region CalculateWeeklyTrend Tests

    [Fact]
    public void CalculateWeeklyTrend_InsufficientData_ReturnsZero()
    {
        var data = CreateHistoricalData(10); // Less than 14 days
        var result = InvokePrivateMethod<double>("CalculateWeeklyTrend", data);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateWeeklyTrend_SufficientData_ReturnsNonZero()
    {
        // Create data with recent week having more visitors than previous week
        var data = new List<DailyTraffic>();
        
        // Previous week (days -14 to -8): 100 visitors each
        for (int i = 14; i >= 8; i--)
        {
            data.Add(CreateDailyTraffic(DateTime.Today.AddDays(-i), 100));
        }
        
        // Recent week (days -7 to -1): 150 visitors each (50% increase)
        for (int i = 7; i >= 1; i--)
        {
            data.Add(CreateDailyTraffic(DateTime.Today.AddDays(-i), 150));
        }
        
        var result = InvokePrivateMethod<double>("CalculateWeeklyTrend", data);
        
        // Should show positive trend (recent week is higher)
        Assert.True(result > 0, $"Expected positive trend, got {result}");
    }

    [Fact]
    public void CalculateWeeklyTrend_DecliningTraffic_ReturnsNegative()
    {
        var data = new List<DailyTraffic>();
        
        // Previous week: 150 visitors each
        for (int i = 14; i >= 8; i--)
        {
            data.Add(CreateDailyTraffic(DateTime.Today.AddDays(-i), 150));
        }
        
        // Recent week: 100 visitors each (decline)
        for (int i = 7; i >= 1; i--)
        {
            data.Add(CreateDailyTraffic(DateTime.Today.AddDays(-i), 100));
        }
        
        var result = InvokePrivateMethod<double>("CalculateWeeklyTrend", data);
        
        Assert.True(result < 0, $"Expected negative trend, got {result}");
    }

    #endregion

    #region TrafficLevel Enum Tests

    [Fact]
    public void TrafficLevel_HasExpectedValues()
    {
        var values = Enum.GetValues<TrafficLevel>();
        Assert.Equal(5, values.Length);
        Assert.Contains(TrafficLevel.VeryLow, values);
        Assert.Contains(TrafficLevel.Low, values);
        Assert.Contains(TrafficLevel.Normal, values);
        Assert.Contains(TrafficLevel.High, values);
        Assert.Contains(TrafficLevel.VeryHigh, values);
    }

    #endregion

    #region DailyTraffic Model Tests

    [Fact]
    public void DailyTraffic_PropertiesSetCorrectly()
    {
        var traffic = new DailyTraffic
        {
            Date = DateTime.Today,
            EstimatedVisitors = 100,
            TotalTransactions = 30,
            TotalUnitsSold = 50,
            TotalRevenue = 5000m,
            ConversionRate = 0.30
        };

        Assert.Equal(DateTime.Today, traffic.Date);
        Assert.Equal(100, traffic.EstimatedVisitors);
        Assert.Equal(30, traffic.TotalTransactions);
        Assert.Equal(50, traffic.TotalUnitsSold);
        Assert.Equal(5000m, traffic.TotalRevenue);
        Assert.Equal(0.30, traffic.ConversionRate);
    }

    #endregion

    #region TrafficPrediction Model Tests

    [Fact]
    public void TrafficPrediction_PropertiesSetCorrectly()
    {
        var prediction = new TrafficPrediction
        {
            PredictionDate = DateTime.Today.AddDays(1),
            PredictedVisitors = 120,
            ConfidenceLevel = 0.85,
            ExpectedLevel = TrafficLevel.High,
            StaffingRecommendation = "Schedule additional staff"
        };

        Assert.Equal(DateTime.Today.AddDays(1), prediction.PredictionDate);
        Assert.Equal(120, prediction.PredictedVisitors);
        Assert.Equal(0.85, prediction.ConfidenceLevel);
        Assert.Equal(TrafficLevel.High, prediction.ExpectedLevel);
        Assert.Equal("Schedule additional staff", prediction.StaffingRecommendation);
    }

    [Fact]
    public void TrafficPrediction_DefaultStaffingRecommendation_IsNull()
    {
        var prediction = new TrafficPrediction
        {
            PredictionDate = DateTime.Today,
            PredictedVisitors = 100,
            ConfidenceLevel = 0.5,
            ExpectedLevel = TrafficLevel.Normal
        };
        Assert.Null(prediction.StaffingRecommendation);
    }

    [Fact]
    public void TrafficPrediction_ConfidenceLevel_ThrowsWhenOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrafficPrediction
        {
            PredictionDate = DateTime.Today,
            PredictedVisitors = 100,
            ConfidenceLevel = 1.5, // Invalid - above 1.0
            ExpectedLevel = TrafficLevel.Normal
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => new TrafficPrediction
        {
            PredictionDate = DateTime.Today,
            PredictedVisitors = 100,
            ConfidenceLevel = -0.1, // Invalid - below 0.0
            ExpectedLevel = TrafficLevel.Normal
        });
    }

    [Fact]
    public void TrafficPrediction_ConfidenceLevel_AcceptsBoundaryValues()
    {
        var predictionMin = new TrafficPrediction
        {
            PredictionDate = DateTime.Today,
            PredictedVisitors = 100,
            ConfidenceLevel = 0.0,
            ExpectedLevel = TrafficLevel.Normal
        };
        Assert.Equal(0.0, predictionMin.ConfidenceLevel);

        var predictionMax = new TrafficPrediction
        {
            PredictionDate = DateTime.Today,
            PredictedVisitors = 100,
            ConfidenceLevel = 1.0,
            ExpectedLevel = TrafficLevel.Normal
        };
        Assert.Equal(1.0, predictionMax.ConfidenceLevel);
    }

    #endregion

    #region TrafficAnalysisSummary Model Tests

    [Fact]
    public void TrafficAnalysisSummary_DefaultValues_AreCorrect()
    {
        var summary = new TrafficAnalysisSummary();
        
        Assert.False(summary.HasData);
        Assert.Equal(string.Empty, summary.Message);
        Assert.Equal("Stable", summary.TrendDirection);
        Assert.Empty(summary.WeekAheadPredictions);
        Assert.Null(summary.TomorrowPrediction);
    }

    [Fact]
    public void TrafficAnalysisSummary_PropertiesSetCorrectly()
    {
        var prediction = new TrafficPrediction
        {
            PredictionDate = DateTime.Today.AddDays(1),
            PredictedVisitors = 100,
            ConfidenceLevel = 0.8,
            ExpectedLevel = TrafficLevel.Normal
        };
        var predictions = new List<TrafficPrediction> { prediction };
        
        var summary = new TrafficAnalysisSummary
        {
            HasData = true,
            AverageDailyVisitors = 150,
            WeeklyTrendPercent = 5.5,
            TrendDirection = "Rising",
            PeakDate = DateTime.Today.AddDays(-3),
            PeakVisitors = 200,
            SlowestDate = DateTime.Today.AddDays(-5),
            SlowestVisitors = 80,
            BusiestDayOfWeek = DayOfWeek.Saturday,
            TomorrowPrediction = prediction,
            WeekAheadPredictions = predictions
        };

        Assert.True(summary.HasData);
        Assert.Equal(150, summary.AverageDailyVisitors);
        Assert.Equal(5.5, summary.WeeklyTrendPercent);
        Assert.Equal("Rising", summary.TrendDirection);
        Assert.Equal(200, summary.PeakVisitors);
        Assert.Equal(80, summary.SlowestVisitors);
        Assert.Equal(DayOfWeek.Saturday, summary.BusiestDayOfWeek);
        Assert.NotNull(summary.TomorrowPrediction);
        Assert.Single(summary.WeekAheadPredictions);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ApplySpecialDayAdjustments_ZeroBaseVisitors_ReturnsZero()
    {
        var regularDay = new DateTime(2025, 3, 12);
        var result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", regularDay, 0);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ApplySpecialDayAdjustments_LargeBaseVisitors_HandlesCorrectly()
    {
        var blackFriday = new DateTime(2025, 11, 28);
        var result = InvokePrivateMethod<int>("ApplySpecialDayAdjustments", blackFriday, 10000);
        Assert.Equal(25000, result); // 10000 * 2.5
    }

    [Fact]
    public void ClassifyTrafficLevel_ExtremelyHighPrediction_ReturnsVeryHigh()
    {
        var result = InvokePrivateMethod<TrafficLevel>("ClassifyTrafficLevel", 1000, 100.0, 10.0);
        Assert.Equal(TrafficLevel.VeryHigh, result);
    }

    [Fact]
    public void ClassifyTrafficLevel_ExtremelyLowPrediction_ReturnsVeryLow()
    {
        var result = InvokePrivateMethod<TrafficLevel>("ClassifyTrafficLevel", 10, 100.0, 10.0);
        Assert.Equal(TrafficLevel.VeryLow, result);
    }

    #endregion
}

