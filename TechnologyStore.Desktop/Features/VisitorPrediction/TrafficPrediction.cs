namespace TechnologyStore.Desktop.Features.VisitorPrediction;

/// <summary>
/// Contains prediction results for future store traffic.
/// This is an immutable record for thread-safety and data integrity.
/// </summary>
public record TrafficPrediction
{
    /// <summary>
    /// The date for which this prediction applies
    /// </summary>
    public required DateTime PredictionDate { get; init; }
    
    /// <summary>
    /// The predicted number of visitors for the date
    /// </summary>
    public required int PredictedVisitors { get; init; }
    
    private readonly double _confidenceLevel;
    
    /// <summary>
    /// Confidence level of the prediction, constrained between 0.0 and 1.0
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is outside 0.0-1.0 range</exception>
    public required double ConfidenceLevel
    {
        get => _confidenceLevel;
        init => _confidenceLevel = value is >= 0.0 and <= 1.0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(ConfidenceLevel), value, "Confidence level must be between 0.0 and 1.0");
    }
    
    /// <summary>
    /// Categorical classification of expected traffic volume
    /// </summary>
    public required TrafficLevel ExpectedLevel { get; init; }
    
    /// <summary>
    /// Optional staffing recommendation based on the prediction
    /// </summary>
    public string? StaffingRecommendation { get; init; }
}

/// <summary>
/// Represents categorical levels of store traffic volume
/// </summary>
public enum TrafficLevel
{
    /// <summary>Significantly below average traffic</summary>
    VeryLow,
    /// <summary>Below average traffic</summary>
    Low,
    /// <summary>Average/typical traffic levels</summary>
    Normal,
    /// <summary>Above average traffic</summary>
    High,
    /// <summary>Significantly above average traffic (peak periods)</summary>
    VeryHigh
}
