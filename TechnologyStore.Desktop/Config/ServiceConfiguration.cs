using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechnologyStore.Desktop.Services;
using TechnologyStore.Desktop.Features.Auth;
using TechnologyStore.Desktop.Features.Email;
using TechnologyStore.Desktop.Features.Leave;
using TechnologyStore.Desktop.Features.Reporting;
using TechnologyStore.Desktop.Features.Products;
using TechnologyStore.Desktop.Features.Products.Data;
using TechnologyStore.Desktop.Features.VisitorPrediction;
using TechnologyStore.Desktop.Features.TimeTracking;
using TechnologyStore.Desktop.UI.Forms;

namespace TechnologyStore.Desktop.Config;

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
        services.AddSingleton(appSettings.Email);

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
            var trendCalculator = sp.GetRequiredService<ITrendCalculator>();
            var recommendationEngine = sp.GetRequiredService<IRecommendationEngine>();
            return new ProductRepository(settings.ConnectionString, trendCalculator, recommendationEngine);
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
        services.AddSingleton<ITrendCalculator, TrendCalculatorService>();

        services.AddSingleton<IVisitorCountPredictor>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new VisitorCountPredictor(settings.ConnectionString);
        });

        services.AddSingleton<ILifecycleSentinel>(sp =>
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
            var lifecycleSentinel = sp.GetRequiredService<ILifecycleSentinel>();
            // Note: IPurchaseOrderService is registered later, so we defer resolution
            return new BackgroundJobService(settings.ConnectionString, repository, lifecycleSentinel, null);
        });

        // Register health check service
        services.AddSingleton<IHealthCheckService>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new HealthCheckService(settings.ConnectionString);
        });

        // Register authentication services
        services.AddSingleton<IUserRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new UserRepository(settings.ConnectionString);
        });
        services.AddSingleton<IAuthenticationService, AuthenticationService>();

        // Register leave management services
        services.AddSingleton<ILeaveRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new LeaveRepository(settings.ConnectionString);
        });

        // Register sales reporting service
        services.AddSingleton<ISalesReportService>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new SalesReportService(settings.ConnectionString);
        });

        // Register time tracking services
        services.AddSingleton<ITimeTrackingRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new TimeTrackingRepository(settings.ConnectionString);
        });
        services.AddSingleton<IWorkShiftRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new WorkShiftRepository(settings.ConnectionString);
        });
        services.AddSingleton<ITimeTrackingService, TimeTrackingService>();

        // Register order repository (for order management)
        services.AddSingleton<TechnologyStore.Shared.Interfaces.IOrderRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new TechnologyStore.Shared.Services.OrderRepository(settings.ConnectionString);
        });

        // Register purchasing services
        services.AddSingleton<TechnologyStore.Shared.Interfaces.ISupplierRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new TechnologyStore.Shared.Services.SupplierRepository(settings.ConnectionString);
        });

        services.AddSingleton<TechnologyStore.Shared.Interfaces.IPurchaseOrderRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new TechnologyStore.Shared.Services.PurchaseOrderRepository(settings.ConnectionString);
        });

        services.AddSingleton<TechnologyStore.Shared.Interfaces.IPurchaseOrderService>(sp =>
        {
            var poRepo = sp.GetRequiredService<TechnologyStore.Shared.Interfaces.IPurchaseOrderRepository>();
            var supplierRepo = sp.GetRequiredService<TechnologyStore.Shared.Interfaces.ISupplierRepository>();
            // Create Shared versions of dependencies
            var dbSettings = sp.GetRequiredService<DatabaseSettings>();
            // Create Shared TrendCalculator and RecommendationEngine
            var sharedTrendCalc = new TechnologyStore.Shared.Services.TrendCalculatorService();
            var sharedRecEngine = new TechnologyStore.Shared.Services.RecommendationEngine();
            var sharedProductRepo = new TechnologyStore.Shared.Services.ProductRepository(
                dbSettings.ConnectionString, sharedTrendCalc, sharedRecEngine);
            // Wrap Desktop email service as Shared.IEmailService  
            var desktopEmailService = sp.GetRequiredService<IEmailService>();
            var sharedEmailService = new SharedEmailServiceWrapper(desktopEmailService);
            // Use Shared.BusinessRuleSettings
            var desktopRules = sp.GetRequiredService<BusinessRuleSettings>();
            var businessRules = new TechnologyStore.Shared.Config.BusinessRuleSettings
            {
                CriticalRunwayDays = desktopRules.CriticalRunwayDays,
                UrgentRunwayDays = desktopRules.UrgentRunwayDays,
                ReorderRunwayDays = desktopRules.ReorderRunwayDays,
                AdequateRunwayDays = desktopRules.AdequateRunwayDays
            };
            return new TechnologyStore.Shared.Services.PurchaseOrderService(
                poRepo, supplierRepo, sharedProductRepo, sharedEmailService, businessRules);
        });

        // Register email service
        services.AddSingleton<IEmailService, GmailEmailService>();
        // Register aggregated dependencies for MainForm
        services.AddTransient<RepositoryDependencies>();
        services.AddTransient<MainFormDependencies>(sp =>
        {
            var deps = ActivatorUtilities.CreateInstance<MainFormDependencies>(sp);
            // Manually inject TimeTracking deps since we added them as properties to avoid breaking signature
            deps.ConfigureTimeTracking(
                sp.GetRequiredService<ITimeTrackingService>(),
                sp.GetRequiredService<IWorkShiftRepository>(),
                sp.GetRequiredService<IUserRepository>()
            );
            return deps;
        });

        // Register forms (transient - new instance each time)
        services.AddTransient<LoginForm>();
        services.AddTransient<MainForm>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Validates that required configuration is present
    /// </summary>
    /// <returns>True if the configuration is valid</returns>
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
