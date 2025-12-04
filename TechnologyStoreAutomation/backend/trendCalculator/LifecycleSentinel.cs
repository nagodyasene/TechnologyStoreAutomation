using Microsoft.Extensions.Logging;

namespace TechnologyStoreAutomation.backend.trendCalculator;

public enum LifecyclePhase
{
    Active,
    Legacy,
    Obsolete
}

public class LifecycleSentinel
{
    private static readonly HttpClient HttpClient = new HttpClient();
    private readonly ILogger<LifecycleSentinel> _logger = AppLogger.CreateLogger<LifecycleSentinel>();

    private const string UrlGooglePixel = "https://support.google.com/pixelphone/answer/4457705";
    private const string UrlAppleVintage = "https://support.apple.com/en-us/102772";

    public event Action<string, LifecyclePhase, string>? OnProductStatusChanged;

        public async Task RunDailyAuditAsync()
        {
            // Run these in parallel for speed
            var appleTask = CheckAppleVintageList();
            var googleTask = CheckGooglePixelEOL();

            await Task.WhenAll(appleTask, googleTask);
        }

        private async Task CheckAppleVintageList()
        {
            try
            {
                // 1. Fetch HTML
                string html = await HttpClient.GetStringAsync(UrlAppleVintage);
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                // 2. Parse Logic (Simulated XPath)
                // In reality, you inspect the page source to find the exact ID or Class
                // Example: //div[@id='vintage-products']//li
                var vintageNodes = doc.DocumentNode.SelectNodes("//li[contains(@class, 'product-item')]");

                if (vintageNodes != null)
                {
                    foreach (var node in vintageNodes)
                    {
                        string productName = node.InnerText.Trim();
                        // Fire the event: "iPhone 6 is now Legacy"
                        OnProductStatusChanged?.Invoke(productName, LifecyclePhase.Legacy, "Found on Apple Vintage List");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log silently, don't crash the POS
                _logger.LogWarning(ex, "Apple vintage list check failed");
            }
        }

        private async Task CheckGooglePixelEOL()
        {
            try
            {
                // Logic: Parse the date table from Google's support page
                // If Date < DateTime.Now, trigger Obsolete
                string html = await HttpClient.GetStringAsync(UrlGooglePixel);
                // Parsing logic would go here...
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google Pixel EOL check failed");
            }
        }
}