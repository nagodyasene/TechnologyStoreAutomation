namespace TechnologyStoreAutomation.backend.visitorCountPrediction;

/// <summary>
/// Summary of traffic analysis for dashboard display
/// </summary>
public class TrafficAnalysisSummary
{
    public bool HasData { get; set; }
    public string Message { get; set; } = string.Empty;
    
    // Historical metrics
    public int AverageDailyVisitors { get; set; }
    public double WeeklyTrendPercent { get; set; }
    public string TrendDirection { get; set; } = "Stable";
    
    // Peak analysis
    public DateTime PeakDate { get; set; }
    public int PeakVisitors { get; set; }
    public DateTime SlowestDate { get; set; }
    public int SlowestVisitors { get; set; }
    public DayOfWeek BusiestDayOfWeek { get; set; }
    
    // Predictions
    public TrafficPrediction? TomorrowPrediction { get; set; }
    public IEnumerable<TrafficPrediction> WeekAheadPredictions { get; set; } = Enumerable.Empty<TrafficPrediction>();
}

