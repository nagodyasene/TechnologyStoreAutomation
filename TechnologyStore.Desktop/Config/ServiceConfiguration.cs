using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechnologyStore.Desktop.Services;
using TechnologyStore.Desktop.Features.Auth;
using TechnologyStore.Desktop.Features.Leave;
using TechnologyStore.Desktop.Features.Reporting;
using TechnologyStore.Desktop.Features.Products;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Services;
using TechnologyStore.Shared.Models;
using TechnologyStore.Desktop.Features.VisitorPrediction;
using TechnologyStore.Desktop.Features.TimeTracking;
using TechnologyStore.Desktop.Features.Payroll;
using TechnologyStore.Desktop.UI.Forms;
using TechnologyStore.Shared.Interfaces;
// Resolve ambiguities - Desktop versions take precedence for Desktop-specific features
using IUserRepository = TechnologyStore.Desktop.Features.Auth.IUserRepository;
using IAuthenticationService = TechnologyStore.Desktop.Features.Auth.IAuthenticationService;
using ITimeTrackingRepository = TechnologyStore.Shared.Interfaces.ITimeTrackingRepository;
using IWorkShiftRepository = TechnologyStore.Shared.Interfaces.IWorkShiftRepository;

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

        // Register trend calculator and recommendation engine (Shared services)
        services.AddSingleton<TechnologyStore.Shared.Interfaces.ITrendCalculator, TechnologyStore.Shared.Services.TrendCalculatorService>();
        services.AddSingleton<TechnologyStore.Shared.Interfaces.IRecommendationEngine, TechnologyStore.Shared.Services.RecommendationEngine>();

        // Register inner repository (not cached - used internally) - Shared service
        services.AddSingleton<TechnologyStore.Shared.Services.ProductRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            var trendCalculator = sp.GetRequiredService<TechnologyStore.Shared.Interfaces.ITrendCalculator>();
            var recommendationEngine = sp.GetRequiredService<TechnologyStore.Shared.Interfaces.IRecommendationEngine>();
            return new TechnologyStore.Shared.Services.ProductRepository(settings.ConnectionString, trendCalculator, recommendationEngine);
        });

        // Register cached repository as IProductRepository (decorator pattern)
        services.AddSingleton<TechnologyStore.Shared.Interfaces.IProductRepository>(sp =>
        {
            var innerRepository = sp.GetRequiredService<TechnologyStore.Shared.Services.ProductRepository>();
            var cache = sp.GetRequiredService<IMemoryCache>();
            var cachingSettings = sp.GetRequiredService<CachingSettings>();
            var logger = sp.GetRequiredService<ILogger<CachedProductRepository>>();
            return new CachedProductRepository(innerRepository, cache, cachingSettings, logger);
        });

        services.AddSingleton<IVisitorCountPredictor>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new VisitorCountPredictor(settings.ConnectionString);
        });

        services.AddSingleton<ILifecycleSentinel>(sp =>
        {
            var repository = sp.GetRequiredService<TechnologyStore.Shared.Interfaces.IProductRepository>();
            var config = sp.GetRequiredService<IConfiguration>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return new LifecycleSentinel(repository, config, httpClientFactory);
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

        // Register email service (Shared service) - must be before PurchaseOrderService
        services.AddSingleton<TechnologyStore.Shared.Interfaces.IEmailService, TechnologyStore.Shared.Services.GmailEmailService>();

        services.AddSingleton<TechnologyStore.Shared.Interfaces.IPurchaseOrderService>(sp =>
        {
            var poRepo = sp.GetRequiredService<TechnologyStore.Shared.Interfaces.IPurchaseOrderRepository>();
            var supplierRepo = sp.GetRequiredService<TechnologyStore.Shared.Interfaces.ISupplierRepository>();
            // Use registered Shared ProductRepository (the cached one implements Shared.Interfaces.IProductRepository)
            var sharedProductRepo = sp.GetRequiredService<TechnologyStore.Shared.Interfaces.IProductRepository>();
            // Use registered Shared email service
            var sharedEmailService = sp.GetRequiredService<TechnologyStore.Shared.Interfaces.IEmailService>();
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

        // Register background job service (after PurchaseOrderService so it can be injected)
        services.AddSingleton<IBackgroundJobService>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            var repository = sp.GetRequiredService<TechnologyStore.Shared.Interfaces.IProductRepository>();
            var lifecycleSentinel = sp.GetRequiredService<ILifecycleSentinel>();
            // Resolve PurchaseOrderService (may be null if not configured, which is handled in BackgroundJobService)
            var purchaseOrderService = sp.GetService<TechnologyStore.Shared.Interfaces.IPurchaseOrderService>();
            return new BackgroundJobService(settings.ConnectionString, repository, lifecycleSentinel, purchaseOrderService);
        });
        // Register aggregated dependencies for MainForm
        services.AddTransient<RepositoryDependencies>();
        // Register available services
        services.AddScoped<IPayrollService, PayrollService>();

        services.AddTransient<MainFormDependencies>(sp =>
        {
            var deps = ActivatorUtilities.CreateInstance<MainFormDependencies>(sp);
            // Manually inject TimeTracking deps since we added them as properties to avoid breaking signature
            deps.ConfigureTimeTracking(
                sp.GetRequiredService<ITimeTrackingService>(),
                sp.GetRequiredService<IWorkShiftRepository>(),
                sp.GetRequiredService<IUserRepository>(),
                sp.GetRequiredService<IPayrollService>()
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
