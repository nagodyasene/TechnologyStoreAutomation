namespace TechnologyStoreAutomation;

/// <summary>
/// Interface for health check service - enables dependency injection and unit testing
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Runs all health checks and returns an aggregated report
    /// </summary>
    Task<HealthReport> CheckAllAsync();

    /// <summary>
    /// Checks database connectivity and response time
    /// </summary>
    Task<HealthCheckResult> CheckDatabaseAsync();

    /// <summary>
    /// Checks database connection pool status
    /// </summary>
    Task<HealthCheckResult> CheckDatabaseConnectionPoolAsync();

    /// <summary>
    /// Checks available memory
    /// </summary>
    HealthCheckResult CheckMemory();

    /// <summary>
    /// Checks available disk space
    /// </summary>
    HealthCheckResult CheckDiskSpace();

    /// <summary>
    /// Quick database connectivity check (returns true/false)
    /// </summary>
    Task<bool> IsDatabaseAvailableAsync();
}
