using TechnologyStore.Desktop.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechnologyStore.Desktop.Services;
using TechnologyStore.Desktop.UI.Forms;
using System.Globalization;

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

            // Turkish-only UI: force culture to tr-TR (no language switching)
            var tr = new CultureInfo("tr-TR");
            CultureInfo.DefaultThreadCurrentCulture = tr;
            CultureInfo.DefaultThreadCurrentUICulture = tr;
            
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
                    $"Başlatma sırasında kritik bir hata oluştu:\n\n{ex.Message}\n\nUygulama kapatılacak.",
                    "Kritik Hata",
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
                    "Yapılandırma Hatası",
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
            // Ensure background job server is disposed on application exit
            Application.ApplicationExit += (_, _) =>
            {
                if (serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            };

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
                    $"Uyarı: Arka plan iş zamanlaması başarısız.\n\n{ex.Message}\n\nUygulama devam edecek; ancak otomatik görevler çalışmayacak.",
                    "Arka Plan İşleri Uyarısı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
