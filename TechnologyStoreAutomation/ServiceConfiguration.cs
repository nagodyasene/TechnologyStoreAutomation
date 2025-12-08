using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechnologyStoreAutomation.backend;
using TechnologyStoreAutomation.backend.trendCalculator;
using TechnologyStoreAutomation.backend.trendCalculator.data;
using TechnologyStoreAutomation.backend.visitorCountPrediction;

namespace TechnologyStoreAutomation;

/// <summary>
/// Centralized service configuration for dependency injection.
/// Registers all application services with the DI container.
/// </summary>
public static class ServiceConfiguration
{
    private static IConfiguration? _configuration;
    
    /// <summary>
    /// Gets the application configuration
    /// </summary>
    public static IConfiguration Configuration => _configuration ??= BuildConfiguration();
    
    /// <summary>
    /// Builds the configuration from appsettings.json and environment variables
    /// </summary>
    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }
    
    /// <summary>
    /// Configures and builds the service provider with all application services
    /// </summary>
    /// <returns>Configured IServiceProvider</returns>
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        
        // Register configuration
        services.AddSingleton(Configuration);
        
        // Bind and register strongly-typed settings
        var appSettings = new AppSettings();
        Configuration.Bind(appSettings);
        
        // Set connection string from environment variables (sensitive data)
        appSettings.Database.ConnectionString = DatabaseConfig.BuildConnectionStringFromEnv();
        
        services.AddSingleton(appSettings);
        services.AddSingleton(appSettings.Ui);
        services.AddSingleton(appSettings.Database);
        services.AddSingleton(appSettings.Caching);
        services.AddSingleton(appSettings.BackgroundJobs);
        services.AddSingleton(appSettings.BusinessRules);
        services.AddSingleton(appSettings.VisitorPrediction);
        services.AddSingleton(appSettings.Application);
        
        // Register logging from configuration
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(Configuration.GetSection("Logging"));
            builder.AddSimpleConsole(options =>
            {
                options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
                options.SingleLine = true;
            });
        });
        
        // Register memory cache with size limit from configuration
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = appSettings.Caching.SizeLimit;
        });
        
        // Register HttpClient factory
        services.AddHttpClient();
        
        // Register inner repository (not cached - used internally)
        services.AddSingleton<ProductRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new ProductRepository(settings.ConnectionString);
        });
        
        // Register cached repository as IProductRepository (decorator pattern)
        services.AddSingleton<IProductRepository>(sp =>
        {
            var innerRepository = sp.GetRequiredService<ProductRepository>();
            var cache = sp.GetRequiredService<IMemoryCache>();
            var cachingSettings = sp.GetRequiredService<CachingSettings>();
            var logger = sp.GetRequiredService<ILogger<CachedProductRepository>>();
            return new CachedProductRepository(innerRepository, cache, cachingSettings, logger);
        });
        
        // Register business services
        services.AddSingleton<IRecommendationEngine, RecommendationEngine>();
        
        services.AddSingleton<IVisitorCountPredictor>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new VisitorCountPredictor(settings.ConnectionString);
        });
        
        services.AddSingleton<LifecycleSentinel>(sp =>
        {
            var repository = sp.GetRequiredService<IProductRepository>();
            var config = sp.GetRequiredService<IConfiguration>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return new LifecycleSentinel(repository, config, httpClientFactory);
        });
        
        // Register background job service
        services.AddSingleton<IBackgroundJobService>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            var repository = sp.GetRequiredService<IProductRepository>();
            var config = sp.GetRequiredService<IConfiguration>();
            return new BackgroundJobService(settings.ConnectionString, repository, config);
        });
        
        // Register health check service
        services.AddSingleton<HealthCheckService>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new HealthCheckService(settings.ConnectionString);
        });
        
        // Register forms (transient - new instance each time)
        services.AddTransient<MainForm>();
        
        return services.BuildServiceProvider();
    }
    
    /// <summary>
    /// Validates that required configuration is present
    /// </summary>
    /// <returns>True if configuration is valid</returns>
    public static bool ValidateConfiguration()
    {
        var connectionString = DatabaseConfig.BuildConnectionStringFromEnv();
        return !string.IsNullOrWhiteSpace(connectionString);
    }
    
    /// <summary>
    /// Gets a user-friendly error message for missing configuration
    /// </summary>
    public static string GetConfigurationErrorMessage()
    {
        return "Database connection is not configured.\n\n" +
               "Please set one of the following environment variable options:\n" +
               "1) DB_CONNECTION_STRING\n" +
               "2) DATABASE_URL\n" +
               "3) DB_HOST / DB_NAME / DB_USER / DB_PASSWORD (and optional DB_PORT)\n\n" +
               "The application cannot continue without a configured database connection.";
    }
}


