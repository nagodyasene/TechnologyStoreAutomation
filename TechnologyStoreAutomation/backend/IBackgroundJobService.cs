using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation.backend;

/// <summary>
/// Interface for background job service - enables dependency injection and unit testing
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>
    /// Initializes Hangfire and schedules all recurring jobs
    /// </summary>
    void Initialize();

    /// <summary>
    /// Generates daily snapshot for yesterday's data
    /// </summary>
    Task GenerateDailySnapshot();

    /// <summary>
    /// Runs the lifecycle audit to check for vintage/obsolete products
    /// </summary>
    Task RunLifecycleAudit();

    /// <summary>
    /// Cleans up old Hangfire job data
    /// </summary>
    Task CleanupOldHangfireJobs();
}

