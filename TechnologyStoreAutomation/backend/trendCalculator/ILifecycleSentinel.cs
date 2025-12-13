namespace TechnologyStoreAutomation.backend.trendCalculator;

/// <summary>
/// Interface for lifecycle sentinel - enables dependency injection and unit testing
/// </summary>
public interface ILifecycleSentinel
{
    /// <summary>
    /// Runs all manufacturer checks and updates product lifecycle phases in database
    /// </summary>
    Task RunDailyAuditAsync();

    /// <summary>
    /// Event fired when a product's lifecycle status changes
    /// </summary>
    event Action<string, LifecyclePhase, string>? OnProductStatusChanged;
}
