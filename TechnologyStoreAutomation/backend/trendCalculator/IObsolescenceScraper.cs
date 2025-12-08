using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation.backend.trendCalculator;

/// <summary>
/// Interface for obsolescence scraper - enables dependency injection and unit testing
/// </summary>
public interface IObsolescenceScraper
{
    /// <summary>
    /// Checks Apple's vintage and obsolete products list and returns detected products
    /// </summary>
    Task<IEnumerable<(string ProductName, LifecyclePhase Phase)>> ScrapeAppleVintageListAsync();

    /// <summary>
    /// Checks Google Pixel phone end-of-life dates and returns affected products
    /// </summary>
    Task<IEnumerable<(string ProductName, LifecyclePhase Phase, DateTime EolDate)>> CheckGooglePixelEOLAsync();
}

