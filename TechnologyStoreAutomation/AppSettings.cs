namespace TechnologyStoreAutomation;

/// <summary>
/// Root application settings loaded from appsettings.json
/// </summary>
public class AppSettings
{
    public ApplicationSettings Application { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public CachingSettings Caching { get; set; } = new();
    public BackgroundJobSettings BackgroundJobs { get; set; } = new();
    public BusinessRuleSettings BusinessRules { get; set; } = new();
    public VisitorPredictionSettings VisitorPrediction { get; set; } = new();
}

/// <summary>
/// General application settings
/// </summary>
public class ApplicationSettings
{
    public string Name { get; set; } = "TechTrend Automation Dashboard";
    public string Version { get; set; } = "1.0.0";
}

/// <summary>
/// UI-related settings
/// </summary>
public class UiSettings
{
    public int WindowWidth { get; set; } = 1200;
    public int WindowHeight { get; set; } = 700;
    public int ToolbarHeight { get; set; } = 50;
    public int StatusBarHeight { get; set; } = 30;
    public int RefreshIntervalMs { get; set; } = 300000;
}

/// <summary>
/// Database connection and behavior settings
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// Connection string (populated from environment variables)
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of retry attempts for transient failures
    /// </summary>
    public int RetryCount { get; set; } = 3;
    
    /// <summary>
    /// Delay between retries in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Background job scheduling settings
/// </summary>
public class BackgroundJobSettings
{
    public int DailySnapshotHour { get; set; } = 1;
    public int LifecycleAuditHour { get; set; } = 2;
    public string WeeklyCleanupDay { get; set; } = "Sunday";
    public int WeeklyCleanupHour { get; set; } = 3;
}

/// <summary>
/// Business rule thresholds and settings
/// </summary>
public class BusinessRuleSettings
{
    // Stock level thresholds
    public int CriticalRunwayDays { get; set; } = 3;
    public int UrgentRunwayDays { get; set; } = 7;
    public int ReorderRunwayDays { get; set; } = 14;
    public int AdequateRunwayDays { get; set; } = 30;
    
    // Trend thresholds
    public double StrongTrendThreshold { get; set; } = 0.3;
    
    // Lifecycle thresholds
    public int LegacyAgeDays { get; set; } = 365;
    public int ObsoleteAgeDays { get; set; } = 1095;
}

/// <summary>
/// Visitor prediction algorithm settings
/// </summary>
public class VisitorPredictionSettings
{
    public double DefaultConversionRate { get; set; } = 0.30;
    public int HistoricalDataDays { get; set; } = 30;
    public int PredictionDaysAhead { get; set; } = 7;
}

/// <summary>
/// Caching configuration settings
/// </summary>
public class CachingSettings
{
    /// <summary>
    /// Dashboard data cache expiration in seconds
    /// </summary>
    public int DashboardDataExpirationSeconds { get; set; } = 60;
    
    /// <summary>
    /// Product list cache expiration in seconds
    /// </summary>
    public int ProductListExpirationSeconds { get; set; } = 120;
    
    /// <summary>
    /// Sales history cache expiration in seconds
    /// </summary>
    public int SalesHistoryExpirationSeconds { get; set; } = 30;
    
    /// <summary>
    /// Maximum number of items in the cache
    /// </summary>
    public int SizeLimit { get; set; } = 1024;
}

