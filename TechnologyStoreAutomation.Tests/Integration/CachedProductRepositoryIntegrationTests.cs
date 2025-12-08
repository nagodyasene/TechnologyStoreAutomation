using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using TechnologyStoreAutomation.backend;
using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation.Tests.Integration;

/// <summary>
/// Integration tests for CachedProductRepository using a real PostgreSQL database.
/// These tests verify caching behavior with actual database operations.
/// </summary>
[Collection("PostgreSQL")]
public class CachedProductRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private ProductRepository _innerRepository = null!;
    private CachedProductRepository _cachedRepository = null!;
    private IMemoryCache _cache = null!;
    private CachingSettings _cachingSettings = null!;

    public CachedProductRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        
        _innerRepository = new ProductRepository(_fixture.ConnectionString);
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        _cachingSettings = new CachingSettings
        {
            DashboardDataExpirationSeconds = 60,
            ProductListExpirationSeconds = 120,
            SalesHistoryExpirationSeconds = 30,
            SizeLimit = 100
        };
        
        var logger = new Mock<ILogger<CachedProductRepository>>();
        _cachedRepository = new CachedProductRepository(
            _innerRepository, 
            _cache, 
            _cachingSettings, 
            logger.Object);
    }

    public Task DisposeAsync()
    {
        _cache.Dispose();
        return Task.CompletedTask;
    }

    #region Caching Behavior Tests

    [Fact]
    public async Task GetAllProductsAsync_SecondCall_ReturnsCachedData()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();

        // Act - First call should hit database
        var firstCall = (await _cachedRepository.GetAllProductsAsync()).ToList();
        
        // Modify database directly (bypass cache)
        await _innerRepository.UpdateProductPhaseAsync(firstCall.First().Id, "OBSOLETE", "Direct update");
        
        // Second call should return cached data (not see the update)
        var secondCall = (await _cachedRepository.GetAllProductsAsync()).ToList();

        // Assert - cached data should still show original phase
        var cachedProduct = secondCall.First(p => p.Id == firstCall.First().Id);
        Assert.NotEqual("OBSOLETE", cachedProduct.LifecyclePhase);
    }

    [Fact]
    public async Task GetDashboardDataAsync_SecondCall_ReturnsCachedData()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();

        // Act - First call
        var firstCall = (await _cachedRepository.GetDashboardDataAsync()).ToList();
        var firstCallTime = DateTime.Now;

        // Small delay
        await Task.Delay(10);

        // Second call should be instant (cached)
        var secondCall = (await _cachedRepository.GetDashboardDataAsync()).ToList();

        // Assert - both calls return same data
        Assert.Equal(firstCall.Count, secondCall.Count);
    }

    [Fact]
    public async Task RecordSaleAsync_InvalidatesDashboardCache()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _cachedRepository.GetAllProductsAsync()).ToList();
        var product = products.First();

        // Populate cache
        var initialDashboard = (await _cachedRepository.GetDashboardDataAsync()).ToList();
        var initialStock = initialDashboard.First(d => d.Name == product.Name).CurrentStock;

        // Act - Record sale (should invalidate cache)
        await _cachedRepository.RecordSaleAsync(product.Id, 5, product.UnitPrice * 5);

        // Get fresh dashboard data (cache was invalidated)
        var updatedDashboard = (await _cachedRepository.GetDashboardDataAsync()).ToList();
        var updatedStock = updatedDashboard.First(d => d.Name == product.Name).CurrentStock;

        // Assert - stock should be reduced
        Assert.Equal(initialStock - 5, updatedStock);
    }

    [Fact]
    public async Task UpdateProductPhaseAsync_InvalidatesProductCache()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        
        // Populate cache
        var initialProducts = (await _cachedRepository.GetAllProductsAsync()).ToList();
        var product = initialProducts.First(p => p.LifecyclePhase == "ACTIVE");

        // Act - Update phase (should invalidate cache)
        await _cachedRepository.UpdateProductPhaseAsync(product.Id, "LEGACY", "Test");

        // Get fresh data (cache was invalidated)
        var updatedProducts = (await _cachedRepository.GetAllProductsAsync()).ToList();
        var updatedProduct = updatedProducts.First(p => p.Id == product.Id);

        // Assert
        Assert.Equal("LEGACY", updatedProduct.LifecyclePhase);
    }

    [Fact]
    public async Task GetSalesHistoryAsync_CachesPerProduct()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _cachedRepository.GetAllProductsAsync()).ToList();
        var product1 = products[0];
        var product2 = products[1];

        // Record sales for both products
        await _innerRepository.RecordSaleAsync(product1.Id, 1, 100m);
        await _innerRepository.RecordSaleAsync(product2.Id, 2, 200m);

        // Act - Cache both products' history
        var history1 = (await _cachedRepository.GetSalesHistoryAsync(product1.Id, 7)).ToList();
        var history2 = (await _cachedRepository.GetSalesHistoryAsync(product2.Id, 7)).ToList();

        // Assert - Different products have different cache entries
        Assert.Single(history1);
        Assert.Single(history2);
        Assert.Equal(1, history1.First().QuantitySold);
        Assert.Equal(2, history2.First().QuantitySold);
    }

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public async Task ClearAllCaches_RemovesAllCachedData()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();

        // Populate caches
        await _cachedRepository.GetAllProductsAsync();
        await _cachedRepository.GetDashboardDataAsync();

        // Act
        _cachedRepository.ClearAllCaches();

        // Modify data directly
        var products = (await _innerRepository.GetAllProductsAsync()).ToList();
        await _innerRepository.UpdateProductPhaseAsync(products.First().Id, "OBSOLETE", "Test");

        // Get data through cached repo (should fetch fresh data)
        var freshProducts = (await _cachedRepository.GetAllProductsAsync()).ToList();
        var updatedProduct = freshProducts.First(p => p.Id == products.First().Id);

        // Assert - should see the update
        Assert.Equal("OBSOLETE", updatedProduct.LifecyclePhase);
    }

    #endregion

    #region Data Integrity Tests

    [Fact]
    public async Task CachedRepository_MaintainsDataConsistency()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        
        // Act - Multiple operations through cached repository
        var products = (await _cachedRepository.GetAllProductsAsync()).ToList();
        var product = products.First();
        
        // Record multiple sales
        for (int i = 0; i < 5; i++)
        {
            await _cachedRepository.RecordSaleAsync(product.Id, 1, product.UnitPrice);
        }

        // Get fresh data
        var updatedProducts = (await _cachedRepository.GetAllProductsAsync()).ToList();
        var updatedProduct = updatedProducts.First(p => p.Id == product.Id);

        // Assert
        Assert.Equal(product.CurrentStock - 5, updatedProduct.CurrentStock);

        // Verify sales history
        var salesHistory = await _cachedRepository.GetSalesHistoryAsync(product.Id, 7);
        Assert.Equal(5, salesHistory.Count());
    }

    #endregion
}

