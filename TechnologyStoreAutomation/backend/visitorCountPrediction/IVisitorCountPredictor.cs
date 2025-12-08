namespace TechnologyStoreAutomation.backend.visitorCountPrediction;

/// <summary>
/// Interface for visitor count predictor - enables dependency injection and unit testing
/// </summary>
public interface IVisitorCountPredictor
{
    /// <summary>
    /// Gets historical daily traffic estimates based on sales data
    /// </summary>
    Task<IEnumerable<DailyTraffic>> GetHistoricalTrafficAsync(int days = 30);

    /// <summary>
    /// Predicts visitor count for the next N days based on historical trends
    /// </summary>
    Task<IEnumerable<TrafficPrediction>> PredictTrafficAsync(int daysAhead = 7);

    /// <summary>
    /// Gets traffic analysis summary for dashboard display
    /// </summary>
    Task<TrafficAnalysisSummary> GetTrafficSummaryAsync();
}

