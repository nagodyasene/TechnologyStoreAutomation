using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Logging;
using TechnologyStoreAutomation.backend.trendCalculator;
using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation.backend;

/// <summary>
/// Configures and manages background jobs for automated system tasks
/// </summary>
public class BackgroundJobService : IBackgroundJobService
{
    private readonly string _connectionString;
    private readonly IProductRepository _repository;
    private readonly ILifecycleSentinel _lifecycleSentinel;
    private readonly ILogger<BackgroundJobService> _logger;

    public BackgroundJobService(string connectionString, IProductRepository repository, ILifecycleSentinel lifecycleSentinel)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _lifecycleSentinel = lifecycleSentinel ?? throw new ArgumentNullException(nameof(lifecycleSentinel));
        _logger = AppLogger.CreateLogger<BackgroundJobService>();
    }

    /// <summary>
    /// Initializes Hangfire and schedules all recurring jobs
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Configure Hangfire to use PostgreSQL storage
            GlobalConfiguration.Configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options =>
                {
                    options.UseNpgsqlConnection(_connectionString);
                });

            _logger.LogInformation("Hangfire initialized with PostgreSQL storage");

            // Schedule recurring jobs
            ScheduleRecurringJobs();

            _logger.LogInformation("All background jobs scheduled successfully");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize background job service", ex);
        }
    }

    /// <summary>
    /// Schedules all recurring background jobs
    /// </summary>
    private void ScheduleRecurringJobs()
    {
        // Job 1: Generate daily snapshots at 1 AM every day
        RecurringJob.AddOrUpdate(
            "daily-snapshot-generation",
            () => GenerateDailySnapshot(),
            Cron.Daily(1)); // 1:00 AM

        // Job 2: Run lifecycle sentinel at 2 AM every day
        RecurringJob.AddOrUpdate(
            "lifecycle-audit",
            () => RunLifecycleAudit(),
            Cron.Daily(2)); // 2:00 AM

        // Job 3: Optional - Cleanup old job data weekly (Sunday at 3 AM)
        RecurringJob.AddOrUpdate(
            "cleanup-old-jobs",
            () => CleanupOldHangfireJobs(),
            Cron.Weekly(DayOfWeek.Sunday, 3));

        _logger.LogInformation("Scheduled recurring jobs: Daily snapshot at 1:00 AM, Lifecycle audit at 2:00 AM, Weekly cleanup Sunday at 3:00 AM");
    }

    /// <summary>
    /// Generates daily snapshot for yesterday's data
    /// Background job method - must be public for Hangfire
    /// </summary>
    [Queue("default")]
    public async Task GenerateDailySnapshot()
    {
        try
        {
            var yesterday = DateTime.Today.AddDays(-1);
            _logger.LogInformation("Starting daily snapshot generation for {Date}", yesterday);

            await _repository.GenerateDailySnapshotAsync(yesterday);

            _logger.LogInformation("Daily snapshot generated successfully for {Date}", yesterday);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to generate daily snapshot", ex);
        }
    }

    /// <summary>
    /// Runs lifecycle sentinel to check for product lifecycle changes
    /// Background job method - must be public for Hangfire
    /// </summary>
    [Queue("default")]
    public async Task RunLifecycleAudit()
    {
        try
        {
            _logger.LogInformation("Starting lifecycle audit");

            await _lifecycleSentinel.RunDailyAuditAsync();

            _logger.LogInformation("Lifecycle audit completed successfully");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to run lifecycle audit", ex);
        }
    }

    /// <summary>
    /// Cleans up old Hangfire job data to prevent database bloat
    /// Background job method - must be public for Hangfire
    /// </summary>
    [Queue("maintenance")]
    public async Task CleanupOldHangfireJobs()
    {
        try
        {
            _logger.LogInformation("Starting Hangfire job cleanup");

            // Clean up succeeded jobs older than 7 days
            var monitor = JobStorage.Current.GetMonitoringApi();
            var succeededJobs = monitor.SucceededJobs(0, int.MaxValue);

            int deletedCount = 0;
            var cutoffDate = DateTime.UtcNow.AddDays(-7);

            foreach (var job in succeededJobs)
            {
                if (job.Value?.SucceededAt != null && job.Value.SucceededAt < cutoffDate)
                {
                    BackgroundJob.Delete(job.Key);
                    deletedCount++;
                }
            }

            _logger.LogInformation("Hangfire cleanup completed: {DeletedCount} old jobs removed", deletedCount);
            await Task.CompletedTask; // Satisfy async signature
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old Hangfire jobs");
            // Don't throw - cleanup is not critical
        }
    }

    /// <summary>
    /// Triggers an immediate one-time execution of a job (for testing/manual triggers)
    /// </summary>
    public string TriggerImmediateJob(string jobType)
    {
        var jobId = jobType.ToLower() switch
        {
            "snapshot" => BackgroundJob.Enqueue(() => GenerateDailySnapshot()),
            "lifecycle" => BackgroundJob.Enqueue(() => RunLifecycleAudit()),
            "cleanup" => BackgroundJob.Enqueue(() => CleanupOldHangfireJobs()),
            _ => throw new ArgumentException($"Unknown job type: {jobType}")
        };

        _logger.LogInformation("Triggered immediate execution of {JobType} with ID {JobId}", jobType, jobId);
        return jobId;
    }

    /// <summary>
    /// Gets the status of a background job
    /// </summary>
    public static string GetJobStatus(string jobId)
    {
        var monitor = JobStorage.Current.GetMonitoringApi();
        var jobDetails = monitor.JobDetails(jobId);

        if (jobDetails == null)
            return "Job not found";

        return $"State: {jobDetails.History.FirstOrDefault()?.StateName ?? "Unknown"}";
    }
}