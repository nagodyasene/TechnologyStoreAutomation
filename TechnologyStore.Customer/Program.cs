using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechnologyStore.Customer.Config;
using TechnologyStore.Customer.Forms;
using TechnologyStore.Shared.Config;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Services;

namespace TechnologyStore.Customer;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            RunApplication();
        }
        catch (Exception ex)
        {
            var logger = AppLogger.CreateLogger("Program");
            logger.LogCritical(ex, "Fatal error during application startup");

            MessageBox.Show(
                $"A fatal error occurred during startup:\n\n{ex.Message}\n\nThe application will now close.",
                "Fatal Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void RunApplication()
    {
        // Load environment variables from .env file (for local development)
        LoadEnvFile();

        // Validate configuration before proceeding
        if (!CustomerServiceConfiguration.ValidateConfiguration())
        {
            MessageBox.Show(
                CustomerServiceConfiguration.GetConfigurationErrorMessage(),
                "Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // Configure dependency injection
        var serviceProvider = CustomerServiceConfiguration.ConfigureServices();

        // Start the Windows Forms application
        ApplicationConfiguration.Initialize();

        // Show login form first
        var loginForm = serviceProvider.GetRequiredService<CustomerLoginForm>();
        
if (loginForm.ShowDialog() != DialogResult.OK)
        {
            // User cancelled login or closed the form
            return;
        }

        // User is now authenticated (or in guest mode)
        // Show the main catalog form
        var catalogForm = serviceProvider.GetRequiredService<CatalogForm>();
        Application.Run(catalogForm);
    }

    /// <summary>
    /// Loads environment variables from .env file if it exists
    /// </summary>
    private static void LoadEnvFile()
    {
        var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
        if (!File.Exists(envPath))
        {
            // Try parent directories
            envPath = Path.Combine(AppContext.BaseDirectory, "..", ".env");
        }
        
if (!File.Exists(envPath))
        {
            envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env");
        }

        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
                    continue;

                var equalsIndex = trimmedLine.IndexOf('=');
                if (equalsIndex > 0)
                {
                    var key = trimmedLine.Substring(0, equalsIndex).Trim();
                    var value = trimmedLine.Substring(equalsIndex + 1).Trim();
                    
// Remove quotes if present
                    if ((value.StartsWith('"') && value.EndsWith('"')) ||
                        (value.StartsWith('\'') && value.EndsWith('\'')))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }
                    
Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }
}
