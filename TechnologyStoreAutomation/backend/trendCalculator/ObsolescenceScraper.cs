using Microsoft.Extensions.Logging;
using TechnologyStoreAutomation.backend.trendCalculator.data;


namespace TechnologyStoreAutomation.backend.trendCalculator;

public class ObsolescenceScraper
{
    private static readonly ILogger<ObsolescenceScraper> Logger = AppLogger.CreateLogger<ObsolescenceScraper>();
    
    // Connection string for the Worker - read from environment variables to avoid hardcoding
    private static string GetConnectionStringFromEnv()
    {
        var conn = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(conn)) return conn;

        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl) && Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var userInfo = uri.UserInfo.Split(':');
            var user = Uri.UnescapeDataString(userInfo[0]);
            var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port.ToString() : "5432";
            var db = uri.AbsolutePath.TrimStart('/');

            if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass) && !string.IsNullOrWhiteSpace(db))
                return $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
        }

        var hostEnv = Environment.GetEnvironmentVariable("DB_HOST") ?? Environment.GetEnvironmentVariable("PGHOST");
        var dbEnv = Environment.GetEnvironmentVariable("DB_NAME") ?? Environment.GetEnvironmentVariable("PGDATABASE");
        var userEnv = Environment.GetEnvironmentVariable("DB_USER") ?? Environment.GetEnvironmentVariable("PGUSER");
        var passEnv = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? Environment.GetEnvironmentVariable("PGPASSWORD");
        var portEnv = Environment.GetEnvironmentVariable("DB_PORT") ?? Environment.GetEnvironmentVariable("PGPORT") ?? "5432";

        if (string.IsNullOrWhiteSpace(hostEnv) || string.IsNullOrWhiteSpace(dbEnv) || string.IsNullOrWhiteSpace(userEnv) || string.IsNullOrWhiteSpace(passEnv))
            throw new InvalidOperationException("Database connection string not configured via environment variables for ObsolescenceScraper.");

        return $"Host={hostEnv};Port={portEnv};Database={dbEnv};Username={userEnv};Password={passEnv};";
    }
        
    static async Task Main(string[] args)
    {
        // Load local .env if present so this worker can be run in local dev (gitignored)
        EnvFileLoader.LoadFromFile();

        Logger.LogInformation("Nightly Job starting at {StartTime}", DateTime.Now);
        var conn = GetConnectionStringFromEnv();
        var repo = new ProductRepository(conn);

        try
        {
            // 1. Run The Sentinel (Scrape Logic)
            await RunSentinelAudit(repo);

            // 2. Generate Snapshots (Math Logic)
            // We process "Yesterday" because the day has just finished
            var yesterday = DateTime.Today.AddDays(-1);
            Logger.LogInformation("Generating snapshots for {Date}", yesterday.ToShortDateString());
            await repo.GenerateDailySnapshotAsync(yesterday);

            Logger.LogInformation("Nightly Job completed successfully");
        }
        catch (Exception ex)
        {
            Logger.LogCritical(ex, "Nightly Job failed");
            // In a real app, you would log this to a file or email the admin
        }
    }
    
    private static async Task RunSentinelAudit(ProductRepository repo)
    {
        Logger.LogInformation("Running Sentinel scrapers");
        using (var client = new HttpClient())
        {
            // --- A. CHECK APPLE VINTAGE LIST ---
            try 
            {
                string url = "https://support.apple.com/en-us/102772";
                string html = await client.GetStringAsync(url);
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                // Hypothetical XPath - this finds list items in the main content area
                // Real implementation requires inspecting the live page structure
                var nodes = doc.DocumentNode.SelectNodes("//div[@id='sections']//li"); 

                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        string product = node.InnerText.Trim();
                        // If we find "iPhone 8" in the vintage list, update DB
                        // We assume we have a method to find ID by Name
                        // await repo.UpdateProductPhaseAsync(foundId, "LEGACY", "Found on Apple Vintage List");
                        Logger.LogInformation("Sentinel found vintage item: {ProductName}", product);
                    }
                }
            }
            catch (Exception ex) { Logger.LogWarning(ex, "Apple scrape failed"); }

            // --- B. CHECK GOOGLE DATES ---
            // (Logic would implement the Date Compare we discussed)
        }
    }
    
}