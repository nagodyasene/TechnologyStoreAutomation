namespace TechnologyStoreAutomation.Tests;

/// <summary>
/// Unit tests for HealthCheckService
/// Tests that can run without a database connection
/// </summary>
public class HealthCheckServiceTests
{
    #region Test Helpers
    
    /// <summary>
    /// Builds a dummy connection string for testing purposes.
    /// These credentials are intentionally invalid and used only for unit testing
    /// where no actual database connection is made or expected to succeed.
    /// </summary>
    private static string BuildTestConnectionString(string host = "localhost", int timeout = 30)
    {
        // Using placeholder values that are clearly for testing only
        const string testDatabase = "test_db";
        const string testUser = "test_user";
        // This is a dummy password for testing - no real connection will be established
        var testCredential = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test_credential"));
        
        return $"Host={host};Database={testDatabase};Username={testUser};Password={testCredential};Timeout={timeout}";
    }
    
    #endregion

    #region HealthCheckResult Tests

    [Fact]
    public void HealthCheckResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new HealthCheckResult();

        // Assert
        Assert.Equal(string.Empty, result.Name);
        Assert.Equal(HealthStatus.Healthy, result.Status); // Default enum value
        Assert.Null(result.Description);
        Assert.Null(result.Exception);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void HealthCheckResult_IsHealthy_ReturnsTrueWhenHealthy()
    {
        // Arrange
        var result = new HealthCheckResult { Status = HealthStatus.Healthy };

        // Act & Assert
        Assert.True(result.IsHealthy);
    }

    [Fact]
    public void HealthCheckResult_IsHealthy_ReturnsFalseWhenDegraded()
    {
        // Arrange
        var result = new HealthCheckResult { Status = HealthStatus.Degraded };

        // Act & Assert
        Assert.False(result.IsHealthy);
    }

    [Fact]
    public void HealthCheckResult_IsHealthy_ReturnsFalseWhenUnhealthy()
    {
        // Arrange
        var result = new HealthCheckResult { Status = HealthStatus.Unhealthy };

        // Act & Assert
        Assert.False(result.IsHealthy);
    }

    #endregion

    #region HealthReport Tests

    [Fact]
    public void HealthReport_IsHealthy_ReturnsTrueWhenAllHealthy()
    {
        // Arrange
        var report = new HealthReport
        {
            OverallStatus = HealthStatus.Healthy,
            Results = new List<HealthCheckResult>
            {
                new() { Name = "Test1", Status = HealthStatus.Healthy },
                new() { Name = "Test2", Status = HealthStatus.Healthy }
            }
        };

        // Act & Assert
        Assert.True(report.IsHealthy);
    }

    [Fact]
    public void HealthReport_IsHealthy_ReturnsFalseWhenDegraded()
    {
        // Arrange
        var report = new HealthReport { OverallStatus = HealthStatus.Degraded };

        // Act & Assert
        Assert.False(report.IsHealthy);
    }

    [Fact]
    public void HealthReport_GetSummary_ContainsAllResults()
    {
        // Arrange
        var report = new HealthReport
        {
            OverallStatus = HealthStatus.Healthy,
            CheckedAt = DateTime.Now,
            TotalDuration = TimeSpan.FromMilliseconds(100),
            Results = new List<HealthCheckResult>
            {
                new() { Name = "Database", Status = HealthStatus.Healthy, Duration = TimeSpan.FromMilliseconds(50) },
                new() { Name = "Memory", Status = HealthStatus.Degraded, Duration = TimeSpan.FromMilliseconds(10) }
            }
        };

        // Act
        var summary = report.GetSummary();

        // Assert
        Assert.Contains("Database", summary);
        Assert.Contains("Memory", summary);
        Assert.Contains("Healthy", summary);
        Assert.Contains("Degraded", summary);
        Assert.Contains("✅", summary); // Healthy icon
        Assert.Contains("⚠️", summary); // Degraded icon
    }

    [Fact]
    public void HealthReport_GetSummary_IncludesExceptionMessage()
    {
        // Arrange
        var report = new HealthReport
        {
            OverallStatus = HealthStatus.Unhealthy,
            CheckedAt = DateTime.Now,
            TotalDuration = TimeSpan.FromMilliseconds(100),
            Results = new List<HealthCheckResult>
            {
                new() 
                { 
                    Name = "Database", 
                    Status = HealthStatus.Unhealthy, 
                    Duration = TimeSpan.FromMilliseconds(50),
                    Exception = new Exception("Connection refused")
                }
            }
        };

        // Act
        var summary = report.GetSummary();

        // Assert
        Assert.Contains("Connection refused", summary);
        Assert.Contains("❌", summary); // Unhealthy icon
    }

    #endregion

    #region HealthCheckService Non-Database Tests

    [Fact]
    public void CheckMemory_ReturnsValidResult()
    {
        // Arrange
        var service = new HealthCheckService(BuildTestConnectionString());

        // Act
        var result = service.CheckMemory();

        // Assert
        Assert.Equal("Memory", result.Name);
        Assert.True(result.Status == HealthStatus.Healthy || result.Status == HealthStatus.Degraded);
        Assert.NotNull(result.Description);
        Assert.True(result.Data.ContainsKey("WorkingSetMB"));
        Assert.True(result.Data.ContainsKey("GCMemoryMB"));
    }

    [Fact]
    public void CheckDiskSpace_ReturnsValidResult()
    {
        // Arrange
        var service = new HealthCheckService(BuildTestConnectionString());

        // Act
        var result = service.CheckDiskSpace();

        // Assert
        Assert.Equal("Disk Space", result.Name);
        Assert.NotNull(result.Description);
        // Should have disk space data unless there's an error
        if (result.Exception == null)
        {
            Assert.True(result.Data.ContainsKey("FreeSpaceGB"));
            Assert.True(result.Data.ContainsKey("TotalSpaceGB"));
        }
    }

    [Fact]
    public async Task CheckDatabaseAsync_InvalidConnection_ReturnsUnhealthy()
    {
        // Arrange
        var service = new HealthCheckService(BuildTestConnectionString(host: "invalid-host-that-does-not-exist", timeout: 1));

        // Act
        var result = await service.CheckDatabaseAsync();

        // Assert
        Assert.Equal("Database", result.Name);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
        Assert.Contains("failed", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IsDatabaseAvailableAsync_InvalidConnection_ReturnsFalse()
    {
        // Arrange
        var service = new HealthCheckService(BuildTestConnectionString(host: "invalid-host-that-does-not-exist", timeout: 1));

        // Act
        var isAvailable = await service.IsDatabaseAvailableAsync();

        // Assert
        Assert.False(isAvailable);
    }

    #endregion

    #region HealthStatus Enum Tests

    [Fact]
    public void HealthStatus_HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)HealthStatus.Healthy);
        Assert.Equal(1, (int)HealthStatus.Degraded);
        Assert.Equal(2, (int)HealthStatus.Unhealthy);
    }

    #endregion
}

