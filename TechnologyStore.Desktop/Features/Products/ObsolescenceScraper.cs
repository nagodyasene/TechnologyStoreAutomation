using TechnologyStore.Desktop.Services;
// using Microsoft.Extensions.Logging;
// using TechnologyStore.Desktop.Features.Products.Data;


// namespace TechnologyStore.Desktop.Features.Products;

// public static class ObsolescenceScraper
// {
//     private static readonly ILogger Logger = AppLogger.CreateLogger(nameof(ObsolescenceScraper));

//     // Get URL from environment variable or configuration
//     private static string GetAppleVintageListUrl()
//     {
//         return Environment.GetEnvironmentVariable("APPLE_VINTAGE_LIST_UR") 
//             ?? "https://support.apple.com/en-us/102772";
//     }

//     // Connection string for the Worker - read from environment variables to avoid hardcoding
//     private static string GetConnectionStringFromEnv()
//     {
//         var conn = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
//         if (!string.IsNullOrWhiteSpace(conn)) return conn;

//         var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
//         if (!string.IsNullOrWhiteSpace(databaseUrl) && Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.UserInfo))
//         {
//             var userInfo = uri.UserInfo.Split(':');
//             var user = Uri.UnescapeDataString(userInfo[0]);
//             var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
//             var host = uri.Host;
//             var port = uri.Port > 0 ? uri.Port.ToString() : "5432";
//             var db = uri.AbsolutePath.TrimStart('/');

//             if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass) && !string.IsNullOrWhiteSpace(db))
//                 return $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
//         }

//         var hostEnv = Environment.GetEnvironmentVariable("DB_HOST") ?? Environment.GetEnvironmentVariable("PGHOST");
//         var dbEnv = Environment.GetEnvironmentVariable("DB_NAME") ?? Environment.GetEnvironmentVariable("PGDATABASE");
//         var userEnv = Environment.GetEnvironmentVariable("DB_USER") ?? Environment.GetEnvironmentVariable("PGUSER");
//         var passEnv = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? Environment.GetEnvironmentVariable("PGPASSWORD");
//         var portEnv = Environment.GetEnvironmentVariable("DB_PORT") ?? Environment.GetEnvironmentVariable("PGPORT") ?? "5432";

//         if (string.IsNullOrWhiteSpace(hostEnv) || string.IsNullOrWhiteSpace(dbEnv) || string.IsNullOrWhiteSpace(userEnv) || string.IsNullOrWhiteSpace(passEnv))
//             throw new InvalidOperationException("Database connection string not configured via environment variables for ObsolescenceScraper.");

//         return $"Host={hostEnv};Port={portEnv};Database={dbEnv};Username={userEnv};Password={passEnv};";
//     }

//     static async Task Main(string[] args)
//     {
//         // Load local .env if present so this worker can be run in local dev (gitignored)
//         EnvFileLoader.LoadFromFile();

//         Logger.LogInformation("Nightly Job starting at {StartTime}", DateTime.Now);
//         var conn = GetConnectionStringFromEnv();
//         var repo = new ProductRepository(conn);

//         try
//         {
//             // 1. Run The Sentinel (Scrape Logic)
//             await RunSentinelAudit();

//             // 2. Generate Snapshots (Math Logic)
//             // We process "Yesterday" because the day has just finished
//             var yesterday = DateTime.Today.AddDays(-1);
//             Logger.LogInformation("Generating snapshots for {Date}", yesterday.ToShortDateString());
//             await repo.GenerateDailySnapshotAsync(yesterday);

//             Logger.LogInformation("Nightly Job completed successfully");
//         }
//         catch (Exception ex)
//         {
//             Logger.LogCritical(ex, "Nightly Job failed");
//             // In a real app, you would log this to a file or email the admin
//         }
//     }


//     private static async Task RunSentinelAudit()
//     {
//         Logger.LogInformation("Running Sentinel scrapers");
//         using (var client = new HttpClient())
//         {
//             // --- A. CHECK APPLE VINTAGE LIST ---
//             ry 
//             {
//                 string html = await client.GetStringAsync(GetAppleVintageListUrl());
//                 var doc = new HtmlAgilityPack.HtmlDocument();
//                 doc.LoadHtml(html);

//                 // Hypothetical XPath - this finds list items in the main content area
//                 // Real implementation requires inspecting the live page structure
//                 var nodes = doc.DocumentNode.SelectNodes("//div[@id='sections']//li");

//                 if (nodes.Count > 0)
//                 {
//                     foreach (var node in nodes)
//                     {
//                         string product = node.InnerText.Trim();
//                         Logger.LogInformation("Sentinel found vintage item: {ProductName}", product);
//                     }
//                 }
//             }
//             catch (Exception ex) { Logger.LogWarning(ex, "Apple scrape failed"); }

//             // --- B. CHECK GOOGLE DATES ---
//             // (Logic would implement the Date Compare we discussed)
//         }
//     }

// }