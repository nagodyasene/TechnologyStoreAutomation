namespace TechnologyStoreAutomation.backend.visitorCountPrediction;

/// <summary>
/// Contains prediction results for future store traffic
/// </summary>
public class TrafficPrediction
{
    public DateTime PredictionDate { get; set; }
    public int PredictedVisitors { get; set; }
    public double ConfidenceLevel { get; set; } // 0.0 to 1.0
    public TrafficLevel ExpectedLevel { get; set; }
    public string StaffingRecommendation { get; set; } = string.Empty;
}

public enum TrafficLevel
{
    VeryLow,
    Low,
    Normal,
    High,
    VeryHigh
}
