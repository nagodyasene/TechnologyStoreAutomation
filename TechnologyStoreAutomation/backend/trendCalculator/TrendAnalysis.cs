namespace TechnologyStoreAutomation.backend.trendCalculator;

/// <summary>
/// Contains the result of trend analysis for a product
/// </summary>
public class TrendAnalysis
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public double DailySalesAverage { get; set; }
    public int RunwayDays { get; set; }
    public TrendDirection Direction { get; set; }
    public double TrendStrength { get; set; } // -1.0 to 1.0 (negative = declining, positive = growing)
    public bool IsAccelerating { get; set; }
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
}

public enum TrendDirection
{
    Stable,
    Rising,
    Falling,
    Volatile
}

