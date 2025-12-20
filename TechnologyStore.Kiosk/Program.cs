using System;
using System.Windows.Forms;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models; // Add Models if used
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace TechnologyStore.Kiosk
{
    static class Program
    {
        public static IConfiguration Configuration { get; private set; }
        public static IServiceProvider ServiceProvider { get; private set; }

        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Build Configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            Configuration = builder.Build();

            // Setup DI
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Run with Attract Form
            Application.Run(ServiceProvider.GetRequiredService<AttractForm>());
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Database
            var connectionString = Configuration.GetConnectionString("DefaultConnection")
                                   ?? "Host=localhost;Database=techstore;Username=postgres;Password=password";

            // Register Repositories (Reusing Shared/Desktop ones if accessible, or re-implementing logic if needed)
            // Note: Since ProductRepository is in TechnologyStore.Desktop, we might need to reference that project 
            // OR move Repositories to Shared. For now, we will create a Kiosk-specific ProductRepository or reference Shared.
            // Wait, ProductRepository is in Desktop project. We cannot reference Desktop from Kiosk (circular or bad practice).
            // We should ideally move ProductRepository to Shared. 
            // For this task, to avoid massive refactor, I will create a simple ProductRepository in Kiosk or duplicate the logic using Dapper.

            services.AddScoped<IProductRepository, KioskProductRepository>();
            services.AddScoped<AttractForm>();
            services.AddScoped<ScanForm>();
        }
    }
}
