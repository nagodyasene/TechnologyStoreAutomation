using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TechnologyStore.Desktop.Services;

/// <summary>
/// Health check result containing status and details
/// </summary>
public class HealthCheckResult
{
    public string Name { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();

    public bool IsHealthy => Status == HealthStatus.Healthy;
}

/// <summary>
/// Overall health status
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Aggregated health report for all checks
/// </summary>
public class HealthReport
{
    public HealthStatus OverallStatus { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public DateTime CheckedAt { get; set; }
    public List<HealthCheckResult> Results { get; set; } = new();

    public bool IsHealthy => OverallStatus == HealthStatus.Healthy;

    /// <summary>
    /// Gets a formatted summary string
    /// </summary>
    public string GetSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Sağlık Kontrol Raporu - {GetStatusTr(OverallStatus)}");
        sb.AppendLine($"Kontrol zamanı: {CheckedAt:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine($"Toplam süre: {TotalDuration.TotalMilliseconds:F0}ms");
        sb.AppendLine();

        foreach (var result in Results)
        {
            sb.AppendLine($"{result.Name}: {GetStatusTr(result.Status)} ({result.Duration.TotalMilliseconds:F0}ms)");

            if (!string.IsNullOrEmpty(result.Description))
                sb.AppendLine($"   {result.Description}");

            if (result.Exception != null)
                sb.AppendLine($"   Hata: {result.Exception.Message}");
        }

        return sb.ToString();
    }

    private static string GetStatusTr(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Healthy => "Sağlıklı",
            HealthStatus.Degraded => "Kısmen Sorunlu",
            HealthStatus.Unhealthy => "Sorunlu",
            _ => status.ToString()
        };
    }
}

/// <summary>
/// Service for performing health checks on application dependencies
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly string _connectionString;
    private readonly ILogger<HealthCheckService> _logger;

    // Thresholds for health status
    private const int DatabaseResponseTimeHealthyMs = 100;
    private const int DatabaseResponseTimeDegradedMs = 500;

    public HealthCheckService(string connectionString)
    {
        _connectionString = connectionString;
        _logger = AppLogger.CreateLogger<HealthCheckService>();
    }

    /// <summary>
    /// Runs all health checks and returns an aggregated report
    /// </summary>
    public async Task<HealthReport> CheckAllAsync()
    {
        var overallStopwatch = Stopwatch.StartNew();
        var results = new List<HealthCheckResult>();

        _logger.LogInformation("Starting health checks");

        // Run all health checks
        results.Add(await CheckDatabaseAsync());
        results.Add(await CheckDatabaseConnectionPoolAsync());
        results.Add(CheckMemory());
        results.Add(CheckDiskSpace());

        overallStopwatch.Stop();

        // Determine overall status (worst status wins)
        HealthStatus overallStatus;
        if (results.Any(r => r.Status == HealthStatus.Unhealthy))
        {
            overallStatus = HealthStatus.Unhealthy;
        }
        else if (results.Any(r => r.Status == HealthStatus.Degraded))
        {
            overallStatus = HealthStatus.Degraded;
        }
        else
        {
            overallStatus = HealthStatus.Healthy;
        }

        var report = new HealthReport
        {
            OverallStatus = overallStatus,
            TotalDuration = overallStopwatch.Elapsed,
            CheckedAt = DateTime.Now,
            Results = results
        };

        _logger.LogInformation("Health checks completed: {Status} in {Duration}ms",
            overallStatus, overallStopwatch.ElapsedMilliseconds);

        return report;
    }

    /// <summary>
    /// Checks database connectivity and response time
    /// </summary>
    public async Task<HealthCheckResult> CheckDatabaseAsync()
    {
        var result = new HealthCheckResult { Name = "Veritabanı" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Simple query to verify database is responsive
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync();

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            // Determine status based on response time
            if (stopwatch.ElapsedMilliseconds <= DatabaseResponseTimeHealthyMs)
            {
                result.Status = HealthStatus.Healthy;
                result.Description = $"Veritabanı normal yanıt veriyor ({stopwatch.ElapsedMilliseconds}ms)";
            }
            else if (stopwatch.ElapsedMilliseconds <= DatabaseResponseTimeDegradedMs)
            {
                result.Status = HealthStatus.Degraded;
                result.Description = $"Veritabanı yavaş yanıt veriyor ({stopwatch.ElapsedMilliseconds}ms)";
            }
            else
            {
                result.Status = HealthStatus.Degraded;
                result.Description = $"Veritabanı çok yavaş yanıt veriyor ({stopwatch.ElapsedMilliseconds}ms)";
            }

            // Add connection info
            result.Data["Host"] = connection.Host ?? "unknown";
            result.Data["Database"] = connection.Database;
            result.Data["ServerVersion"] = connection.ServerVersion;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Status = HealthStatus.Unhealthy;
            result.Description = "Veritabanı bağlantısı başarısız";
            result.Exception = ex;

            _logger.LogError(ex, "Database health check failed");
        }

        return result;
    }

    /// <summary>
    /// Checks database connection pool status
    /// </summary>
    public async Task<HealthCheckResult> CheckDatabaseConnectionPoolAsync()
    {
        var result = new HealthCheckResult { Name = "Bağlantı Havuzu" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Open multiple connections to test pool
            var connections = new List<NpgsqlConnection>();
            const int testConnections = 3;

            for (int i = 0; i < testConnections; i++)
            {
                var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                connections.Add(conn);
            }

            // Close all connections
            foreach (var conn in connections)
            {
                await conn.CloseAsync();
                await conn.DisposeAsync();
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Status = HealthStatus.Healthy;
            result.Description = $"Connection pool healthy ({testConnections} connections tested)";
            result.Data["TestedConnections"] = testConnections;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Status = HealthStatus.Unhealthy;
            result.Description = "Bağlantı havuzu tükendi veya kullanılamıyor";
            result.Exception = ex;

            _logger.LogError(ex, "Connection pool health check failed");
        }

        return result;
    }

    /// <summary>
    /// Checks available memory
    /// </summary>
    public HealthCheckResult CheckMemory()
    {
        var result = new HealthCheckResult { Name = "Bellek" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var process = Process.GetCurrentProcess();
            var workingSetMb = process.WorkingSet64 / (1024 * 1024);
            var gcMemoryMb = GC.GetTotalMemory(false) / (1024 * 1024);

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            // Check if memory usage is concerning (> 500MB for this app type)
            if (workingSetMb < 256)
            {
                result.Status = HealthStatus.Healthy;
                result.Description = $"Bellek kullanımı normal ({workingSetMb}MB)";
            }
            else if (workingSetMb < 512)
            {
                result.Status = HealthStatus.Degraded;
                result.Description = $"Bellek kullanımı yüksek ({workingSetMb}MB)";
            }
            else
            {
                result.Status = HealthStatus.Degraded;
                result.Description = $"Bellek kullanımı çok yüksek ({workingSetMb}MB)";
            }

            result.Data["WorkingSetMB"] = workingSetMb;
            result.Data["GCMemoryMB"] = gcMemoryMb;
            result.Data["Gen0Collections"] = GC.CollectionCount(0);
            result.Data["Gen1Collections"] = GC.CollectionCount(1);
            result.Data["Gen2Collections"] = GC.CollectionCount(2);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Status = HealthStatus.Degraded;
            result.Description = "Bellek bilgisi alınamadı";
            result.Exception = ex;
        }

        return result;
    }

    /// <summary>
    /// Checks available disk space
    /// </summary>
    public HealthCheckResult CheckDiskSpace()
    {
        var result = new HealthCheckResult { Name = "Disk Alanı" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var appPath = AppContext.BaseDirectory;
            var driveInfo = new DriveInfo(Path.GetPathRoot(appPath) ?? "C:\\");

            var freeSpaceGb = driveInfo.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var totalSpaceGb = driveInfo.TotalSize / (1024.0 * 1024 * 1024);
            var usedPercent = ((totalSpaceGb - freeSpaceGb) / totalSpaceGb) * 100;

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (freeSpaceGb > 10)
            {
                result.Status = HealthStatus.Healthy;
                result.Description = $"Disk alanı yeterli ({freeSpaceGb:F1}GB boş)";
            }
            else if (freeSpaceGb > 2)
            {
                result.Status = HealthStatus.Degraded;
                result.Description = $"Disk alanı düşük ({freeSpaceGb:F1}GB boş)";
            }
            else
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"Disk alanı kritik ({freeSpaceGb:F1}GB boş)";
            }

            result.Data["DriveName"] = driveInfo.Name;
            result.Data["FreeSpaceGB"] = Math.Round(freeSpaceGb, 2);
            result.Data["TotalSpaceGB"] = Math.Round(totalSpaceGb, 2);
            result.Data["UsedPercent"] = Math.Round(usedPercent, 1);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Status = HealthStatus.Degraded;
            result.Description = "Disk alanı bilgisi alınamadı";
            result.Exception = ex;
        }

        return result;
    }

    /// <summary>
    /// Quick database connectivity check (returns true/false)
    /// </summary>
    public async Task<bool> IsDatabaseAvailableAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

