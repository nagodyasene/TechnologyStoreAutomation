using TechnologyStore.Desktop.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechnologyStore.Desktop.Services;
using TechnologyStore.Desktop.UI.Forms;
using DesktopDatabaseConfig = TechnologyStore.Desktop.Config.DatabaseConfig;

namespace TechnologyStore.Desktop
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Initialize global exception handler first (before anything else can fail)
            GlobalExceptionHandler.Initialize();
            
            try
            {
                RunApplication();
            }
            catch (Exception ex)
            {
                // Last resort exception handler
                var logger = AppLogger.CreateLogger("Program");
                logger.LogCritical(ex, "Fatal error during application startup");
                
                MessageBox.Show(
                    $"A fatal error occurred during startup:\n\n{ex.Message}\n\nThe application will now close.",
                    "Fatal Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Main application logic separated for cleaner exception handling
        /// </summary>
        private static void RunApplication()
        {
            // Load environment variables from .env file (for local development)
            LoadEnvFile();

            // Validate configuration before proceeding
            if (!ServiceConfiguration.ValidateConfiguration())
            {
                MessageBox.Show(
                    ServiceConfiguration.GetConfigurationErrorMessage(),
                    "Configuration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Configure dependency injection
            var serviceProvider = ServiceConfiguration.ConfigureServices();
            
            // Re-initialize exception handler with proper logger from DI
            var logger = serviceProvider.GetService<ILogger<MainForm>>();
            if (logger != null)
            {
                GlobalExceptionHandler.Initialize(logger);
            }

            // Run database migrations to fix enum issues (synchronously to avoid async void issues)
            try
            {
                RunDatabaseMigrationsAsync(serviceProvider).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // Log but don't block - migrations are non-critical
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                var migrationLogger = loggerFactory?.CreateLogger("DatabaseMigration") 
                    ?? AppLogger.CreateLogger("DatabaseMigration");
                migrationLogger.LogError(ex, "Database migration failed, continuing anyway");
            }

            // Initialize background job service
            InitializeBackgroundJobs(serviceProvider);

            // Start the Windows Forms application
            ApplicationConfiguration.Initialize();
            
            // Show login form first
            var loginForm = serviceProvider.GetRequiredService<LoginForm>();
            if (loginForm.ShowDialog() != DialogResult.OK)
            {
                // User cancelled login or closed the form
                return;
            }

            // Resolve MainForm from DI container (user is now authenticated)
            var mainForm = serviceProvider.GetRequiredService<MainForm>();
            Application.Run(mainForm);
        }

        /// <summary>
        /// Loads environment variables from .env file if it exists
        /// Searches in multiple common locations
        /// </summary>
        private static void LoadEnvFile()
        {
            var searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, ".env"),
                Path.Combine(AppContext.BaseDirectory, "Config", ".env"),
                Path.Combine(AppContext.BaseDirectory, "..", ".env"),
                Path.Combine(AppContext.BaseDirectory, "..", "Config", ".env"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Config", ".env")
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    EnvFileLoader.LoadFromFile(path);
                    return;
                }
            }
        }

        /// <summary>
        /// Runs database migrations to fix schema issues (like missing enum values)
        /// </summary>
        private static async Task RunDatabaseMigrationsAsync(IServiceProvider serviceProvider)
        {
            try
            {
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger("DatabaseMigration") 
                    ?? AppLogger.CreateLogger("DatabaseMigration");
                
                DatabaseMigration.Initialize(logger);
                
                var connectionString = DesktopDatabaseConfig.BuildConnectionStringFromEnv();
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    logger.LogWarning("Cannot run database migrations: connection string not configured");
                    return;
                }

                logger.LogInformation("Running database migrations...");
                var success = await DatabaseMigration.FixTimeEntryTypeEnumAsync(connectionString);
                
                if (success)
                {
                    logger.LogInformation("Database migrations completed successfully");
                }
                else
                {
                    logger.LogWarning("Database migrations completed with warnings. Some enum values may still be missing.");
                }
            }
            catch (Exception ex)
            {
                // Log error but don't block application startup
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger("DatabaseMigration") 
                    ?? AppLogger.CreateLogger("DatabaseMigration");
                logger.LogError(ex, "Failed to run database migrations");
                
                // Don't show a message box here - migrations are non-critical for startup
            }
        }

        /// <summary>
        /// Initializes background jobs with proper error handling
        /// </summary>
        private static void InitializeBackgroundJobs(IServiceProvider serviceProvider)
        {
            try
            {
                var backgroundJobService = serviceProvider.GetRequiredService<IBackgroundJobService>();
                backgroundJobService.Initialize();
            }
            catch (Exception ex)
            {
                // Log error and continue - background jobs are not critical for UI functionality
                GlobalExceptionHandler.ReportException(ex, "Background Job Initialization");
                
                MessageBox.Show(
                    $"Warning: Background job scheduling failed.\n\n{ex.Message}\n\nThe application will continue, but automated tasks won't run.",
                    "Background Jobs Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
