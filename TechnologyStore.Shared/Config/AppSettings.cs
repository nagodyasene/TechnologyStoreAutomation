namespace TechnologyStore.Shared.Config;

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
    public EmailSettings Email { get; set; } = new();
    public StoreSettings Store { get; set; } = new();
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

/// <summary>
/// Email configuration settings for Gmail API integration
/// </summary>
public class EmailSettings
{
    /// <summary>
    /// When true, emails are logged instead of being sent (for development/testing)
    /// </summary>
    public bool TestMode { get; set; } = true;

    /// <summary>
    /// Email address used as the sender for outgoing emails
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// Path to the Gmail API OAuth credentials file (credentials.json)
    /// </summary>
    public string GmailCredentialsPath { get; set; } = "credentials.json";

    /// <summary>
    /// Path where the OAuth token will be stored after first authorization
    /// </summary>
    public string TokenStorePath { get; set; } = "token";
}

/// <summary>
/// Store information for invoices and pickup
/// </summary>
public class StoreSettings
{
    public string Name { get; set; } = "Technology Store";
    public string Address { get; set; } = "123 Tech Avenue, City, Country";
    public string Phone { get; set; } = "+1 (555) 123-4567";
    public string Email { get; set; } = "store@example.com";
    public string OpeningHours { get; set; } = "Mon-Sat: 9AM-8PM, Sun: 10AM-6PM";
    public decimal TaxRate { get; set; } = 0.10m; // 10% tax
}
