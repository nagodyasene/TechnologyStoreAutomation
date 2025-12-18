using TechnologyStore.Desktop.Services;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TechnologyStore.Desktop.Features.Products.Data;

namespace TechnologyStore.Desktop.Features.Products;

public enum LifecyclePhase
{
    Active,
    Legacy,
    Obsolete
}

/// <summary>
/// Monitors manufacturer websites to automatically detect when products become vintage/obsolete
/// </summary>
public class LifecycleSentinel : ILifecycleSentinel
{
    private readonly HttpClient _httpClient;
    private readonly IProductRepository _repository;
    private readonly ILogger<LifecycleSentinel> _logger;
    private readonly string _urlAppleVintage;

    // Event for external subscribers (optional, mainly for testing)
    public event Action<string, LifecyclePhase, string>? OnProductStatusChanged;

    public LifecycleSentinel(IProductRepository repository, IConfiguration configuration, IHttpClientFactory? httpClientFactory = null)
    {
        _repository = repository;
        _logger = AppLogger.CreateLogger<LifecycleSentinel>();
        _httpClient = httpClientFactory?.CreateClient() ?? new HttpClient();

        // Read URLs from configuration
        _urlAppleVintage = configuration["Manufacturers:Apple:VintageListUrl"]
            ?? throw new InvalidOperationException("Manufacturers:Apple:VintageListUrl configuration is required");

        // Note: Google Pixel EOL URL is configured but not currently used
        // (using hardcoded EOL dates instead until JS-rendered page scraping is implemented)

        // Configure HttpClient timeout and headers
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    /// <summary>
    /// Runs all manufacturer checks and updates product lifecycle phases in database
    /// </summary>
    public async Task RunDailyAuditAsync()
    {
        _logger.LogInformation("Starting daily lifecycle audit");

        try
        {
            // Run manufacturer checks in parallel for speed
            var appleTask = CheckAppleVintageList();
            var googleTask = CheckGooglePixelEol();

            await Task.WhenAll(appleTask, googleTask);

            _logger.LogInformation("Daily lifecycle audit completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daily lifecycle audit failed");
        }
    }

    /// <summary>
    /// Checks Apple's vintage and obsolete products list
    /// </summary>
    private async Task CheckAppleVintageList()
    {
        try
        {
            _logger.LogInformation("Checking Apple vintage list at {Url}", _urlAppleVintage);

            string html = await _httpClient.GetStringAsync(_urlAppleVintage);
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Apple lists products in simple <ul><li> structures
            // Products are organized by device type (Mac, iPhone, iPad, etc.)
            var allListItems = doc.DocumentNode.SelectNodes("//ul/li");

            if (allListItems == null || !allListItems.Any())
            {
                _logger.LogWarning("No list items found on Apple vintage page");
                return;
            }

            var (vintageCount, obsoleteCount) = await ProcessAppleListItems(allListItems);

            _logger.LogInformation(
                "Apple check complete: {VintageCount} vintage, {ObsoleteCount} obsolete products found",
                vintageCount, obsoleteCount);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Apple vintage list (network error)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Apple vintage list");
        }
    }

    /// <summary>
    /// Processes list items from Apple's vintage page and updates product phases
    /// </summary>
    private async Task<(int VintageCount, int ObsoleteCount)> ProcessAppleListItems(HtmlAgilityPack.HtmlNodeCollection allListItems)
    {
        int vintageCount = 0;
        int obsoleteCount = 0;
        bool inVintageSection = false;
        bool inObsoleteSection = false;

        foreach (var item in allListItems)
        {
            string text = item.InnerText.Trim();

            // Skip empty items
            if (string.IsNullOrWhiteSpace(text)) continue;

            // Update section state based on section headers
            UpdateSectionState(text, ref inVintageSection, ref inObsoleteSection);

            // Process product if in a recognized section
            if ((inVintageSection || inObsoleteSection) && IsProductName(text))
            {
                await ProcessAppleProduct(text, inObsoleteSection);

                if (inVintageSection) vintageCount++;
                else obsoleteCount++;
            }
        }

        return (vintageCount, obsoleteCount);
    }

    /// <summary>
    /// Updates the section state based on section header detection
    /// </summary>
    private static void UpdateSectionState(string text, ref bool inVintageSection, ref bool inObsoleteSection)
    {
        if (text.Contains("Vintage", StringComparison.OrdinalIgnoreCase))
        {
            inVintageSection = true;
            inObsoleteSection = false;
        }
        else if (text.Contains("Obsolete", StringComparison.OrdinalIgnoreCase))
        {
            inObsoleteSection = true;
            inVintageSection = false;
        }
    }

    /// <summary>
    /// Processes a single Apple product and updates its phase
    /// </summary>
    private async Task ProcessAppleProduct(string productName, bool isObsolete)
    {
        var phase = isObsolete ? LifecyclePhase.Obsolete : LifecyclePhase.Legacy;
        var phaseString = isObsolete ? "OBSOLETE" : "LEGACY";
        var reason = $"Found on Apple {phaseString} list";

        await UpdateProductPhaseByName(productName, phaseString, reason);

        // Fire event for subscribers
        OnProductStatusChanged?.Invoke(productName, phase, reason);
    }

    /// <summary>
    /// Checks Google Pixel phone end-of-life dates
    /// Note: Google's page is JavaScript-rendered, so this uses a fallback approach
    /// with known EOL dates until a proper JS scraper is implemented
    /// </summary>
    private async Task CheckGooglePixelEol()
    {
        try
        {
            _logger.LogInformation("Checking Google Pixel EOL dates");

            // Fallback: Use known Pixel EOL dates (updated as of Dec 2025)
            // Source: https://support.google.com/pixelphone/answer/4457705
            var pixelEolDates = new Dictionary<string, DateTime>
            {
                { "Pixel", new DateTime(2018, 12, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel XL", new DateTime(2018, 12, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 2", new DateTime(2020, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 2 XL", new DateTime(2020, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 3", new DateTime(2022, 5, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 3 XL", new DateTime(2022, 5, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 3a", new DateTime(2022, 5, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 3a XL", new DateTime(2022, 5, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 4", new DateTime(2023, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 4 XL", new DateTime(2023, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 4a", new DateTime(2023, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 5", new DateTime(2024, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 5a", new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 6", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 6 Pro", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 6a", new DateTime(2027, 7, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 7", new DateTime(2027, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 7 Pro", new DateTime(2027, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 7a", new DateTime(2028, 5, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 8", new DateTime(2030, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                { "Pixel 8 Pro", new DateTime(2030, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
            };

            var now = DateTime.UtcNow;
            int obsoleteCount = 0;
            int legacyCount = 0;

            foreach (var (productName, eolDate) in pixelEolDates)
            {
                if (eolDate < now)
                {
                    // Past EOL = OBSOLETE
                    await UpdateProductPhaseByName(productName, "OBSOLETE",
                        $"Google support ended {eolDate:yyyy-MM-dd}");
                    OnProductStatusChanged?.Invoke(productName, LifecyclePhase.Obsolete,
                        $"Support ended {eolDate:yyyy-MM-dd}");
                    obsoleteCount++;
                }
                else if (eolDate < now.AddMonths(6))
                {
                    // EOL within 6 months = LEGACY
                    await UpdateProductPhaseByName(productName, "LEGACY",
                        $"Google support ending {eolDate:yyyy-MM-dd}");
                    OnProductStatusChanged?.Invoke(productName, LifecyclePhase.Legacy,
                        $"Support ending {eolDate:yyyy-MM-dd}");
                    legacyCount++;
                }
            }

            _logger.LogInformation(
                "Google Pixel check complete: {LegacyCount} legacy, {ObsoleteCount} obsolete products found",
                legacyCount, obsoleteCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Google Pixel EOL dates");
        }
    }

    /// <summary>
    /// Updates product phase in database by matching product name (fuzzy matching)
    /// </summary>
    private async Task UpdateProductPhaseByName(string scrapedName, string newPhase, string reason)
    {
        try
        {
            // Get all products from database
            var allProducts = await _repository.GetAllProductsAsync();

            // Try to find matching product by name (case-insensitive, fuzzy match)
            var matchingProduct = allProducts.FirstOrDefault(p =>
                FuzzyMatchProductName(p.Name, scrapedName));

            if (matchingProduct != null)
            {
                // Only update if phase is actually changing
                if (matchingProduct.LifecyclePhase != newPhase)
                {
                    await _repository.UpdateProductPhaseAsync(matchingProduct.Id, newPhase, reason);
                    _logger.LogInformation(
                        "Updated product '{ProductName}' (ID: {ProductId}) to {Phase}: {Reason}",
                        matchingProduct.Name, matchingProduct.Id, newPhase, reason);
                }
            }
            else
            {
                _logger.LogDebug("No matching product found for '{ScrapedName}'", scrapedName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update product phase for '{ScrapedName}'", scrapedName);
        }
    }

    /// <summary>
    /// Fuzzy matches product names to handle variations
    /// Example: "iPhone 12" matches "Apple iPhone 12 128GB"
    /// </summary>
    private static bool FuzzyMatchProductName(string dbProductName, string scrapedName)
    {
        if (string.IsNullOrWhiteSpace(dbProductName) || string.IsNullOrWhiteSpace(scrapedName))
            return false;

        // Normalize both names
        var dbNormalized = NormalizeProductName(dbProductName);
        var scrapedNormalized = NormalizeProductName(scrapedName);

        // Exact match after normalization
        if (dbNormalized.Equals(scrapedNormalized, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if scraped name is contained in DB name (e.g., "iPhone 12" in "Apple iPhone 12 128GB")
        if (dbNormalized.Contains(scrapedNormalized, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if DB name is contained in scraped name (e.g., "iPhone 12" in "iPhone 12 Pro Max")
        if (scrapedNormalized.Contains(dbNormalized, StringComparison.OrdinalIgnoreCase))
            return true;

        // Extract key model identifiers and compare
        var dbModel = ExtractModelIdentifier(dbNormalized);
        var scrapedModel = ExtractModelIdentifier(scrapedNormalized);

        return !string.IsNullOrEmpty(dbModel) && !string.IsNullOrEmpty(scrapedModel) &&
               dbModel.Equals(scrapedModel, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes product name by removing common variations
    /// </summary>
    private static string NormalizeProductName(string name)
    {
        var timeout = TimeSpan.FromMilliseconds(100);

        // Remove common prefixes/suffixes
        name = Regex.Replace(name, @"\b(Apple|Google|Samsung|Sony)\b", "", RegexOptions.IgnoreCase, timeout);

        // Remove storage capacities
        name = Regex.Replace(name, @"\b\d+\s*(GB|TB|MB)\b", "", RegexOptions.IgnoreCase, timeout);

        // Remove colors
        name = Regex.Replace(name, @"\b(Black|White|Silver|Gold|Blue|Green|Red|Pink|Purple|Gray|Grey|Space Gray)\b",
            "", RegexOptions.IgnoreCase, timeout);

        // Remove extra whitespace
        name = Regex.Replace(name, @"\s+", " ", RegexOptions.None, timeout);

        return name.Trim();
    }

    /// <summary>
    /// Extracts core model identifier (e.g., "iPhone 12 Pro" from "Apple iPhone 12 Pro 256GB Silver")
    /// </summary>
    private static string ExtractModelIdentifier(string name)
    {
        var timeout = TimeSpan.FromMilliseconds(100);

        // Match patterns like "iPhone 12", "Pixel 6 Pro", "MacBook Air (2020)"
        var patterns = new[]
        {
            @"iPhone\s+\d+\s*(Pro|Max|Plus|Mini)?",
            @"Pixel\s+\d+\s*(Pro|XL|a)?",
            @"MacBook\s+(Pro|Air)\s*\([^)]+\)",
            @"iPad\s+(Pro|Air|Mini)?\s*\d*"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(name, pattern, RegexOptions.IgnoreCase, timeout);
            if (match.Success)
                return match.Value.Trim();
        }

        return name;
    }

    /// <summary>
    /// Determines if text is likely a product name (not a section header or description)
    /// </summary>
    private static bool IsProductName(string text)
    {
        // Filter out section headers and non-product text
        if (text.Length < 5) return false;
        if (text.StartsWith("Vintage", StringComparison.OrdinalIgnoreCase)) return false;
        if (text.StartsWith("Obsolete", StringComparison.OrdinalIgnoreCase)) return false;
        if (text.StartsWith("Products that", StringComparison.OrdinalIgnoreCase)) return false;
        if (text.Contains("Apple has designated", StringComparison.OrdinalIgnoreCase)) return false;

        // Must contain common product identifiers
        var productKeywords = new[] { "iPhone", "iPad", "MacBook", "iMac", "Mac", "Apple Watch", "AirPods", "Pixel" };
        return productKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}