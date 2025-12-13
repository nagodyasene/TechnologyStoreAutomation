using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation.backend;

/// <summary>
/// Caching decorator for IProductRepository.
/// Wraps an existing repository and adds caching for frequently accessed data.
/// </summary>
public class CachedProductRepository : IProductRepository
{
    private readonly IProductRepository _innerRepository;
    private readonly IMemoryCache _cache;
    private readonly CachingSettings _settings;
    private readonly ILogger<CachedProductRepository> _logger;

    // Track active sales history cache keys for efficient invalidation
    private readonly HashSet<string> _activeSalesHistoryKeys = new();

    #region Cache Keys

    private const string DashboardDataKey = "dashboard_data";
    private const string AllProductsKey = "all_products";
    private const string SalesHistoryKeyPrefix = "sales_history_";

    #endregion

    public CachedProductRepository(
        IProductRepository innerRepository,
        IMemoryCache cache,
        CachingSettings settings,
        ILogger<CachedProductRepository> logger)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets dashboard data with caching
    /// </summary>
    public async Task<IEnumerable<ProductDashboardDto>> GetDashboardDataAsync()
    {
        if (_cache.TryGetValue(DashboardDataKey, out IEnumerable<ProductDashboardDto>? cachedData) && cachedData != null)
        {
            _logger.LogDebug("Dashboard data retrieved from cache");
            return cachedData;
        }

        _logger.LogDebug("Dashboard data cache miss - fetching from database");
        var data = (await _innerRepository.GetDashboardDataAsync()).ToList();

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(_settings.DashboardDataExpirationSeconds))
            .SetSize(1);

        _cache.Set(DashboardDataKey, data, cacheOptions);
        _logger.LogDebug("Dashboard data cached for {Seconds} seconds", _settings.DashboardDataExpirationSeconds);

        return data;
    }

    /// <summary>
    /// Gets all products with caching
    /// </summary>
    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        if (_cache.TryGetValue(AllProductsKey, out IEnumerable<Product>? cachedProducts) && cachedProducts != null)
        {
            _logger.LogDebug("Product list retrieved from cache");
            return cachedProducts;
        }

        _logger.LogDebug("Product list cache miss - fetching from database");
        var products = (await _innerRepository.GetAllProductsAsync()).ToList();

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(_settings.ProductListExpirationSeconds))
            .SetSize(1);

        _cache.Set(AllProductsKey, products, cacheOptions);
        _logger.LogDebug("Product list cached for {Seconds} seconds", _settings.ProductListExpirationSeconds);

        return products;
    }

    /// <summary>
    /// Gets sales history with caching (per product)
    /// </summary>
    public async Task<IEnumerable<SalesTransaction>> GetSalesHistoryAsync(int productId, int days = 30)
    {
        var cacheKey = $"{SalesHistoryKeyPrefix}{productId}_{days}";

        if (_cache.TryGetValue(cacheKey, out IEnumerable<SalesTransaction>? cachedHistory) && cachedHistory != null)
        {
            _logger.LogDebug("Sales history for product {ProductId} retrieved from cache", productId);
            return cachedHistory;
        }

        _logger.LogDebug("Sales history cache miss for product {ProductId} - fetching from database", productId);
        var history = (await _innerRepository.GetSalesHistoryAsync(productId, days)).ToList();

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(_settings.SalesHistoryExpirationSeconds))
            .SetSize(1);

        _cache.Set(cacheKey, history, cacheOptions);

        // Track this cache key for efficient invalidation
        lock (_activeSalesHistoryKeys)
        {
            _activeSalesHistoryKeys.Add(cacheKey);
        }

        return history;
    }

    /// <summary>
    /// Records a sale and invalidates relevant caches
    /// </summary>
    public async Task<int> RecordSaleAsync(int productId, int quantitySold, decimal totalAmount, DateTime? saleDate = null)
    {
        var result = await _innerRepository.RecordSaleAsync(productId, quantitySold, totalAmount, saleDate);

        // Invalidate caches that depend on sales data
        InvalidateSalesCaches(productId);

        _logger.LogDebug("Sale recorded and caches invalidated for product {ProductId}", productId);
        return result;
    }

    /// <summary>
    /// Updates product phase and invalidates relevant caches
    /// </summary>
    public async Task UpdateProductPhaseAsync(int productId, string newPhase, string reason)
    {
        await _innerRepository.UpdateProductPhaseAsync(productId, newPhase, reason);

        // Invalidate product-related caches
        InvalidateProductCaches();

        _logger.LogDebug("Product phase updated and caches invalidated for product {ProductId}", productId);
    }

    /// <summary>
    /// Generates daily snapshot (no caching - write operation)
    /// </summary>
    public async Task GenerateDailySnapshotAsync(DateTime dateToProcess)
    {
        await _innerRepository.GenerateDailySnapshotAsync(dateToProcess);

        // Invalidate dashboard cache after snapshot generation
        InvalidateDashboardCache();

        _logger.LogDebug("Daily snapshot generated and dashboard cache invalidated");
    }

    #region Cache Invalidation

    /// <summary>
    /// Invalidates caches related to sales data
    /// </summary>
    private void InvalidateSalesCaches(int productId)
    {
        _cache.Remove(DashboardDataKey);
        _cache.Remove(AllProductsKey);

        // Remove only tracked sales history keys for this product (efficient invalidation)
        lock (_activeSalesHistoryKeys)
        {
            var keysToRemove = _activeSalesHistoryKeys
                .Where(k => k.StartsWith($"{SalesHistoryKeyPrefix}{productId}_"))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
                _activeSalesHistoryKeys.Remove(key);
            }

            _logger.LogDebug("Invalidated {Count} sales history cache entries for product {ProductId}",
                keysToRemove.Count, productId);
        }
    }

    /// <summary>
    /// Invalidates product-related caches
    /// </summary>
    private void InvalidateProductCaches()
    {
        _cache.Remove(DashboardDataKey);
        _cache.Remove(AllProductsKey);
    }

    /// <summary>
    /// Invalidates dashboard cache only
    /// </summary>
    private void InvalidateDashboardCache()
    {
        _cache.Remove(DashboardDataKey);
    }

    /// <summary>
    /// Clears all caches (useful for manual refresh)
    /// </summary>
    public void ClearAllCaches()
    {
        _cache.Remove(DashboardDataKey);
        _cache.Remove(AllProductsKey);
        _logger.LogInformation("All caches cleared");
    }

    #endregion
}

