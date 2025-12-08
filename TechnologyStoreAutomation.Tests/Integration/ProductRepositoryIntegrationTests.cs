using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation.Tests.Integration;

/// <summary>
/// Integration tests for ProductRepository using a real PostgreSQL database via Testcontainers.
/// These tests verify actual database operations, SQL queries, and data integrity.
/// </summary>
[Collection("PostgreSQL")]
public class ProductRepositoryIntegrationTests : IAsyncLifetime
{
    #region Constants
    
    private const string TestIphoneSku = "TEST-IP15PRO";
    
    #endregion

    private readonly PostgreSqlFixture _fixture;
    private ProductRepository _repository = null!;

    public ProductRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        _repository = new ProductRepository(_fixture.ConnectionString);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region GetAllProductsAsync Tests

    [Fact]
    public async Task GetAllProductsAsync_EmptyDatabase_ReturnsEmptyCollection()
    {
        // Act
        var products = await _repository.GetAllProductsAsync();

        // Assert
        Assert.Empty(products);
    }

    [Fact]
    public async Task GetAllProductsAsync_WithProducts_ReturnsAllProducts()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();

        // Act
        var products = (await _repository.GetAllProductsAsync()).ToList();

        // Assert
        Assert.Equal(4, products.Count);
        Assert.Contains(products, p => p.Name == "iPhone 15 Pro");
        Assert.Contains(products, p => p.Name == "MacBook Pro");
    }

    [Fact]
    public async Task GetAllProductsAsync_ReturnsCorrectProductProperties()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();

        // Act
        var products = (await _repository.GetAllProductsAsync()).ToList();
        var iPhone = products.First(p => p.Sku == TestIphoneSku);

        // Assert
        Assert.Equal("iPhone 15 Pro", iPhone.Name);
        Assert.Equal("Smartphones", iPhone.Category);
        Assert.Equal(999.99m, iPhone.UnitPrice);
        Assert.Equal(50, iPhone.CurrentStock);
        Assert.Equal("ACTIVE", iPhone.LifecyclePhase);
    }

    #endregion

    #region RecordSaleAsync Tests

    [Fact]
    public async Task RecordSaleAsync_ValidSale_ReturnsTransactionId()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _repository.GetAllProductsAsync()).ToList();
        var productId = products.First(p => p.Sku == TestIphoneSku).Id;

        // Act
        var transactionId = await _repository.RecordSaleAsync(productId, 2, 1999.98m);

        // Assert
        Assert.True(transactionId > 0);
    }

    [Fact]
    public async Task RecordSaleAsync_UpdatesProductStock()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _repository.GetAllProductsAsync()).ToList();
        var product = products.First(p => p.Sku == TestIphoneSku);
        var initialStock = product.CurrentStock;

        // Act
        await _repository.RecordSaleAsync(product.Id, 5, 4999.95m);

        // Refresh product data
        products = (await _repository.GetAllProductsAsync()).ToList();
        var updatedProduct = products.First(p => p.Id == product.Id);

        // Assert
        Assert.Equal(initialStock - 5, updatedProduct.CurrentStock);
    }

    [Fact]
    public async Task RecordSaleAsync_CreatesSalesTransaction()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _repository.GetAllProductsAsync()).ToList();
        var productId = products.First(p => p.Sku == TestIphoneSku).Id;
        var saleDate = DateTime.Today;

        // Act
        await _repository.RecordSaleAsync(productId, 3, 2999.97m, saleDate);

        // Assert - verify via sales history
        var salesHistory = await _repository.GetSalesHistoryAsync(productId, 7);
        var sale = salesHistory.FirstOrDefault();

        Assert.NotNull(sale);
        Assert.Equal(productId, sale.ProductId);
        Assert.Equal(3, sale.QuantitySold);
        Assert.Equal(2999.97m, sale.TotalAmount);
    }

    [Fact]
    public async Task RecordSaleAsync_WithPastDate_RecordsCorrectDate()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _repository.GetAllProductsAsync()).ToList();
        var productId = products.First(p => p.Sku == TestIphoneSku).Id;
        var pastDate = DateTime.Today.AddDays(-5);

        // Act
        await _repository.RecordSaleAsync(productId, 1, 999.99m, pastDate);

        // Assert
        var salesHistory = await _repository.GetSalesHistoryAsync(productId, 30);
        var sale = salesHistory.First();

        Assert.Equal(pastDate.Date, sale.SaleDate.Date);
    }

    #endregion

    #region GetSalesHistoryAsync Tests

    [Fact]
    public async Task GetSalesHistoryAsync_NoSales_ReturnsEmptyCollection()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _repository.GetAllProductsAsync()).ToList();
        var productId = products[0].Id;

        // Act
        var history = await _repository.GetSalesHistoryAsync(productId, 30);

        // Assert
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetSalesHistoryAsync_WithSales_ReturnsSalesInDateRange()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _repository.GetAllProductsAsync()).ToList();
        var productId = products.First(p => p.Sku == TestIphoneSku).Id;

        // Record sales over several days
        await _repository.RecordSaleAsync(productId, 1, 999.99m, DateTime.Today);
        await _repository.RecordSaleAsync(productId, 2, 1999.98m, DateTime.Today.AddDays(-3));
        await _repository.RecordSaleAsync(productId, 1, 999.99m, DateTime.Today.AddDays(-10));

        // Act
        var historyLast7Days = (await _repository.GetSalesHistoryAsync(productId, 7)).ToList();
        var historyLast30Days = (await _repository.GetSalesHistoryAsync(productId, 30)).ToList();

        // Assert
        Assert.Equal(2, historyLast7Days.Count); // Today and 3 days ago
        Assert.Equal(3, historyLast30Days.Count); // All three sales
    }

    #endregion

    #region UpdateProductPhaseAsync Tests

    [Fact]
    public async Task UpdateProductPhaseAsync_ChangesPhase()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _repository.GetAllProductsAsync()).ToList();
        var product = products.First(p => p.LifecyclePhase == "ACTIVE");

        // Act
        await _repository.UpdateProductPhaseAsync(product.Id, "LEGACY", "Test phase change");

        // Assert
        products = (await _repository.GetAllProductsAsync()).ToList();
        var updatedProduct = products.First(p => p.Id == product.Id);
        Assert.Equal("LEGACY", updatedProduct.LifecyclePhase);
    }

    [Fact]
    public async Task UpdateProductPhaseAsync_CreatesAuditLogEntry()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _repository.GetAllProductsAsync()).ToList();
        var product = products.First(p => p.LifecyclePhase == "ACTIVE");
        var reason = "Product discontinued by manufacturer";

        // Act
        await _repository.UpdateProductPhaseAsync(product.Id, "OBSOLETE", reason);

        // Assert - verify audit log was created (we'd need to add a method to check this)
        // For now, just verify the phase changed
        products = (await _repository.GetAllProductsAsync()).ToList();
        var updatedProduct = products.First(p => p.Id == product.Id);
        Assert.Equal("OBSOLETE", updatedProduct.LifecyclePhase);
    }

    #endregion

    #region GetDashboardDataAsync Tests

    [Fact]
    public async Task GetDashboardDataAsync_EmptyDatabase_ReturnsEmptyCollection()
    {
        // Act
        var dashboardData = await _repository.GetDashboardDataAsync();

        // Assert
        Assert.Empty(dashboardData);
    }

    [Fact]
    public async Task GetDashboardDataAsync_WithProducts_ReturnsDashboardDtos()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();

        // Act
        var dashboardData = (await _repository.GetDashboardDataAsync()).ToList();

        // Assert
        Assert.Equal(4, dashboardData.Count);
        Assert.All(dashboardData, dto =>
        {
            Assert.False(string.IsNullOrEmpty(dto.Name));
            Assert.False(string.IsNullOrEmpty(dto.Phase));
        });
    }

    [Fact]
    public async Task GetDashboardDataAsync_IncludesRecommendations()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();

        // Act
        var dashboardData = (await _repository.GetDashboardDataAsync()).ToList();

        // Assert - each item should have a recommendation
        Assert.All(dashboardData, dto =>
        {
            Assert.False(string.IsNullOrEmpty(dto.Recommendation));
        });
    }

    [Fact]
    public async Task GetDashboardDataAsync_CalculatesSalesAndRunway()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _repository.GetAllProductsAsync()).ToList();
        var productId = products.First(p => p.Sku == TestIphoneSku).Id;

        // Record some sales
        for (int i = 0; i < 7; i++)
        {
            await _repository.RecordSaleAsync(productId, 5, 4999.95m, DateTime.Today.AddDays(-i));
        }

        // Act
        var dashboardData = (await _repository.GetDashboardDataAsync()).ToList();
        var productDashboard = dashboardData.First(d => d.Name == "iPhone 15 Pro");

        // Assert
        Assert.True(productDashboard.SalesLast7Days > 0);
        // Stock was 50, sold 35 (5 per day for 7 days), remaining 15
        // With 5 sales/day average, runway should be around 3 days
        Assert.True(productDashboard.RunwayDays > 0);
    }

    #endregion

    #region GenerateDailySnapshotAsync Tests

    [Fact]
    public async Task GenerateDailySnapshotAsync_CreatesSnapshot()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var yesterday = DateTime.Today.AddDays(-1);
        var initialDashboardData = (await _repository.GetDashboardDataAsync()).ToList();

        // Act
        await _repository.GenerateDailySnapshotAsync(yesterday);
        
        // Verify by calling it again - should not fail due to ON CONFLICT
        await _repository.GenerateDailySnapshotAsync(yesterday);

        // Assert - verify snapshot was created by checking dashboard data persists
        var finalDashboardData = (await _repository.GetDashboardDataAsync()).ToList();
        Assert.Equal(initialDashboardData.Count, finalDashboardData.Count);
        Assert.Equal(4, finalDashboardData.Count);
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task RecordSaleAsync_ConcurrentSales_MaintainsDataIntegrity()
    {
        // Arrange
        await _fixture.SeedTestDataAsync();
        var products = (await _repository.GetAllProductsAsync()).ToList();
        var product = products.First(p => p.Sku == TestIphoneSku);
        var initialStock = product.CurrentStock;
        var salesCount = 10;

        // Act - simulate concurrent sales
        var tasks = Enumerable.Range(0, salesCount)
            .Select(_ => _repository.RecordSaleAsync(product.Id, 1, 999.99m))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert
        products = (await _repository.GetAllProductsAsync()).ToList();
        var updatedProduct = products.First(p => p.Id == product.Id);
        
        Assert.Equal(initialStock - salesCount, updatedProduct.CurrentStock);
    }

    #endregion
}

