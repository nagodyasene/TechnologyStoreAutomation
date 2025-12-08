using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechnologyStoreAutomation.backend;

namespace TechnologyStoreAutomation
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
            EnvFileLoader.LoadFromFile();

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

            // Initialize background job service
            InitializeBackgroundJobs(serviceProvider);

            // Start the Windows Forms application
            ApplicationConfiguration.Initialize();
            
            // Resolve MainForm from DI container
            var mainForm = serviceProvider.GetRequiredService<MainForm>();
            Application.Run(mainForm);
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
