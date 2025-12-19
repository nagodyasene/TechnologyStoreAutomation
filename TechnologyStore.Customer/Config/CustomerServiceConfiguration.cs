using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechnologyStore.Shared.Config;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Services;
using TechnologyStore.Customer.Forms;
using TechnologyStore.Customer.Services;

namespace TechnologyStore.Customer.Config;

/// <summary>
/// Service configuration for the Customer application
/// </summary>
public static class CustomerServiceConfiguration
{
    private static IConfiguration? _configuration;

    public static IConfiguration Configuration => _configuration ??= BuildConfiguration();

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register configuration
        services.AddSingleton(Configuration);

        // Bind and register strongly-typed settings
        var appSettings = new AppSettings();
        Configuration.Bind(appSettings);

        // Set connection string from environment variables
        appSettings.Database.ConnectionString = DatabaseConfig.BuildConnectionStringFromEnv();

        services.AddSingleton(appSettings);
        services.AddSingleton(appSettings.Database);
        services.AddSingleton(appSettings.Caching);
        services.AddSingleton(appSettings.Email);
        services.AddSingleton(appSettings.Store);

        // Register logging
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(Configuration.GetSection("Logging"));
            builder.AddSimpleConsole(options =>
            {
                options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
                options.SingleLine = true;
            });
        });

        // Register memory cache
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = appSettings.Caching.SizeLimit;
        });

        // Register HttpClient factory
        services.AddHttpClient();

        // Register trend calculator and recommendation engine (needed by ProductRepository)
        services.AddSingleton<ITrendCalculator, TrendCalculatorService>();
        services.AddSingleton<IRecommendationEngine, RecommendationEngine>();

        // Register repositories
        services.AddSingleton<IProductRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            var trendCalculator = sp.GetRequiredService<ITrendCalculator>();
            var recommendationEngine = sp.GetRequiredService<IRecommendationEngine>();
            return new ProductRepository(settings.ConnectionString, trendCalculator, recommendationEngine);
        });

        services.AddSingleton<ICustomerRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new CustomerRepository(settings.ConnectionString);
        });

        services.AddSingleton<IOrderRepository>(sp =>
        {
            var settings = sp.GetRequiredService<DatabaseSettings>();
            return new OrderRepository(settings.ConnectionString);
        });

        // Register services
        services.AddSingleton<ICustomerAuthService, CustomerAuthService>();
        services.AddSingleton<IOrderService, OrderService>();
        services.AddSingleton<IEmailService, GmailEmailService>();

        // Register shopping cart (singleton for session)
        services.AddSingleton<ShoppingCartService>();

        // Register invoice generator
        services.AddSingleton<InvoiceGenerator>();

        // Register forms (transient)
        services.AddTransient<CustomerLoginForm>();
        services.AddTransient<CatalogForm>();

        return services.BuildServiceProvider();
    }

    public static bool ValidateConfiguration()
    {
        var connectionString = DatabaseConfig.BuildConnectionStringFromEnv();
        return !string.IsNullOrWhiteSpace(connectionString);
    }

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
