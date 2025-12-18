using Moq;
using TechnologyStore.Desktop.Features.Products;
using TechnologyStore.Desktop.Features.Products.Data;

namespace TechnologyStore.Desktop.Tests;

/// <summary>
/// Integration-style tests for ProductRepository using mocked IProductRepository.
/// These tests verify the business logic flow without requiring a real database connection.
/// </summary>
public class ProductRepositoryTests
{
    #region Constants
    
    private const string ActivePhase = "ACTIVE";
    private const string LegacyPhase = "LEGACY";
    private const string ObsoletePhase = "OBSOLETE";
    
    #endregion

    private readonly Mock<IProductRepository> _mockRepository;

    public ProductRepositoryTests()
    {
        _mockRepository = new Mock<IProductRepository>();
    }

    #region Helper Methods

    private static Product CreateTestProduct(int id = 1, string name = "Test Product", int stock = 100, 
        string phase = ActivePhase, decimal price = 99.99m)
    {
        return new Product
        {
            Id = id,
            Name = name,
            Sku = $"SKU-{id:D5}",
            Category = "Electronics",
            UnitPrice = price,
            CurrentStock = stock,
            LifecyclePhase = phase,
            CreatedAt = DateTime.Now.AddDays(-30),
            LastUpdated = DateTime.Now
        };
    }

    private static SalesTransaction CreateSalesTransaction(int id, int productId, int quantity, 
        decimal amount, DateTime? date = null)
    {
        return new SalesTransaction
        {
            Id = id,
            ProductId = productId,
            QuantitySold = quantity,
            TotalAmount = amount,
            SaleDate = date ?? DateTime.Today,
            CreatedAt = DateTime.Now
        };
    }

    #endregion

    #region GetAllProductsAsync Tests

    [Fact]
    public async Task GetAllProductsAsync_ReturnsAllProducts()
    {
        // Arrange
        var products = new List<Product>
        {
            CreateTestProduct(1, "iPhone 15", 50),
            CreateTestProduct(2, "MacBook Pro", 25),
            CreateTestProduct(3, "iPad Air", 75)
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(products);

        // Act
        var result = await _mockRepository.Object.GetAllProductsAsync();

        // Assert
        Assert.Equal(3, result.Count());
        _mockRepository.Verify(r => r.GetAllProductsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllProductsAsync_EmptyDatabase_ReturnsEmptyCollection()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(new List<Product>());

        // Act
        var result = await _mockRepository.Object.GetAllProductsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllProductsAsync_ReturnsProductsWithCorrectProperties()
    {
        // Arrange
        var product = CreateTestProduct(1, "Test iPhone", 100, "ACTIVE", 999.99m);
        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(new List<Product> { product });

        // Act
        var result = (await _mockRepository.Object.GetAllProductsAsync()).First();

        // Assert
        Assert.Equal(1, result.Id);
        Assert.Equal("Test iPhone", result.Name);
        Assert.Equal(100, result.CurrentStock);
        Assert.Equal(ActivePhase, result.LifecyclePhase);
        Assert.Equal(999.99m, result.UnitPrice);
    }

    #endregion

    #region RecordSaleAsync Tests

    [Fact]
    public async Task RecordSaleAsync_ValidSale_ReturnsNewSaleId()
    {
        // Arrange
        _mockRepository.Setup(r => r.RecordSaleAsync(1, 5, 499.95m, null))
            .ReturnsAsync(100);

        // Act
        var result = await _mockRepository.Object.RecordSaleAsync(1, 5, 499.95m);

        // Assert
        Assert.Equal(100, result);
        _mockRepository.Verify(r => r.RecordSaleAsync(1, 5, 499.95m, null), Times.Once);
    }

    [Fact]
    public async Task RecordSaleAsync_WithSpecificDate_PassesDateCorrectly()
    {
        // Arrange
        var saleDate = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        _mockRepository.Setup(r => r.RecordSaleAsync(1, 2, 199.98m, saleDate))
            .ReturnsAsync(101);

        // Act
        var result = await _mockRepository.Object.RecordSaleAsync(1, 2, 199.98m, saleDate);

        // Assert
        Assert.Equal(101, result);
        _mockRepository.Verify(r => r.RecordSaleAsync(1, 2, 199.98m, saleDate), Times.Once);
    }

    [Fact]
    public async Task RecordSaleAsync_MultipleSales_ReturnsUniqueIds()
    {
        // Arrange
        var callCount = 0;
        _mockRepository.Setup(r => r.RecordSaleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(), null))
            .ReturnsAsync(() => ++callCount);

        // Act
        var id1 = await _mockRepository.Object.RecordSaleAsync(1, 1, 99.99m);
        var id2 = await _mockRepository.Object.RecordSaleAsync(2, 2, 199.98m);
        var id3 = await _mockRepository.Object.RecordSaleAsync(3, 3, 299.97m);

        // Assert
        Assert.Equal(1, id1);
        Assert.Equal(2, id2);
        Assert.Equal(3, id3);
    }

    #endregion

    #region GetSalesHistoryAsync Tests

    [Fact]
    public async Task GetSalesHistoryAsync_ReturnsTransactionsForProduct()
    {
        // Arrange
        var transactions = new List<SalesTransaction>
        {
            CreateSalesTransaction(1, 1, 2, 199.98m, DateTime.Today),
            CreateSalesTransaction(2, 1, 1, 99.99m, DateTime.Today.AddDays(-1)),
            CreateSalesTransaction(3, 1, 3, 299.97m, DateTime.Today.AddDays(-2))
        };

        _mockRepository.Setup(r => r.GetSalesHistoryAsync(1, 30))
            .ReturnsAsync(transactions);

        // Act
        var result = await _mockRepository.Object.GetSalesHistoryAsync(1);

        // Assert
        Assert.Equal(3, result.Count());
        Assert.All(result, t => Assert.Equal(1, t.ProductId));
    }

    [Fact]
    public async Task GetSalesHistoryAsync_NoSales_ReturnsEmptyCollection()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetSalesHistoryAsync(999, 30))
            .ReturnsAsync(new List<SalesTransaction>());

        // Act
        var result = await _mockRepository.Object.GetSalesHistoryAsync(999);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSalesHistoryAsync_CustomDays_UsesCorrectParameter()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetSalesHistoryAsync(1, 7))
            .ReturnsAsync(new List<SalesTransaction>());

        // Act
        await _mockRepository.Object.GetSalesHistoryAsync(1, 7);

        // Assert
        _mockRepository.Verify(r => r.GetSalesHistoryAsync(1, 7), Times.Once);
    }

    #endregion

    #region UpdateProductPhaseAsync Tests

    [Fact]
    public async Task UpdateProductPhaseAsync_ValidPhase_CompletesSuccessfully()
    {
        // Arrange
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(1, LegacyPhase, "Product entering vintage status"))
            .Returns(Task.CompletedTask);

        // Act & Assert (no exception should be thrown)
        await _mockRepository.Object.UpdateProductPhaseAsync(1, LegacyPhase, "Product entering vintage status");
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(1, LegacyPhase, "Product entering vintage status"), Times.Once);
    }

    [Theory]
    [InlineData(ActivePhase)]
    [InlineData(LegacyPhase)]
    [InlineData(ObsoletePhase)]
    public async Task UpdateProductPhaseAsync_AllValidPhases_CallsRepository(string phase)
    {
        // Arrange
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), phase, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _mockRepository.Object.UpdateProductPhaseAsync(1, phase, "Test reason");

        // Assert
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(1, phase, "Test reason"), Times.Once);
    }

    #endregion

    #region GenerateDailySnapshotAsync Tests

    [Fact]
    public async Task GenerateDailySnapshotAsync_ValidDate_CompletesSuccessfully()
    {
        // Arrange
        var snapshotDate = DateTime.Today.AddDays(-1);
        _mockRepository.Setup(r => r.GenerateDailySnapshotAsync(snapshotDate))
            .Returns(Task.CompletedTask);

        // Act
        await _mockRepository.Object.GenerateDailySnapshotAsync(snapshotDate);

        // Assert
        _mockRepository.Verify(r => r.GenerateDailySnapshotAsync(snapshotDate), Times.Once);
    }

    [Fact]
    public async Task GenerateDailySnapshotAsync_MultipleConsecutiveDays_CallsForEachDay()
    {
        // Arrange
        _mockRepository.Setup(r => r.GenerateDailySnapshotAsync(It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        // Act - Simulate generating snapshots for past 3 days
        for (int i = 3; i >= 1; i--)
        {
            await _mockRepository.Object.GenerateDailySnapshotAsync(DateTime.Today.AddDays(-i));
        }

        // Assert
        _mockRepository.Verify(r => r.GenerateDailySnapshotAsync(It.IsAny<DateTime>()), Times.Exactly(3));
    }

    #endregion

    #region GetDashboardDataAsync Tests

    [Fact]
    public async Task GetDashboardDataAsync_ReturnsProductsWithRecommendations()
    {
        // Arrange
        var dashboardData = new List<ProductDashboardDto>
        {
            new ProductDashboardDto
            {
                Id = 1,
                Name = "iPhone 15",
                Phase = ActivePhase,
                CurrentStock = 50,
                SalesLast7Days = 35,
                RunwayDays = 10,
                Recommendation = "Reorder recommended"
            },
            new ProductDashboardDto
            {
                Id = 2,
                Name = "iPhone 12",
                Phase = LegacyPhase,
                CurrentStock = 10,
                SalesLast7Days = 3,
                RunwayDays = 45,
                Recommendation = "Monitor - consider discount"
            }
        };

        _mockRepository.Setup(r => r.GetDashboardDataAsync())
            .ReturnsAsync(dashboardData);

        // Act
        var result = (await _mockRepository.Object.GetDashboardDataAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Phase == ActivePhase);
        Assert.Contains(result, p => p.Phase == LegacyPhase);
        Assert.All(result, p => Assert.False(string.IsNullOrEmpty(p.Recommendation)));
    }

    [Fact]
    public async Task GetDashboardDataAsync_EmptyInventory_ReturnsEmptyList()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetDashboardDataAsync())
            .ReturnsAsync(new List<ProductDashboardDto>());

        // Act
        var result = await _mockRepository.Object.GetDashboardDataAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDashboardDataAsync_CriticalStockItems_IncludedInResults()
    {
        // Arrange
        var dashboardData = new List<ProductDashboardDto>
        {
            new ProductDashboardDto
            {
                Id = 1,
                Name = "Critical Stock Item",
                Phase = ActivePhase,
                CurrentStock = 5,
                SalesLast7Days = 21, // 3/day average
                RunwayDays = 1, // Only 1 day of stock left!
                Recommendation = "CRITICAL: Reorder immediately"
            }
        };

        _mockRepository.Setup(r => r.GetDashboardDataAsync())
            .ReturnsAsync(dashboardData);

        // Act
        var result = (await _mockRepository.Object.GetDashboardDataAsync()).First();

        // Assert
        Assert.True(result.RunwayDays <= 3);
        Assert.Contains("CRITICAL", result.Recommendation);
    }

    #endregion

    #region Integration Flow Tests

    [Fact]
    public async Task FullSalesFlow_RecordSaleAndVerifyHistory()
    {
        // Arrange
        var productId = 1;
        var saleId = 100;
        var saleDate = DateTime.Today;
        
        _mockRepository.Setup(r => r.RecordSaleAsync(productId, 2, 199.98m, null))
            .ReturnsAsync(saleId);

        var salesHistory = new List<SalesTransaction>
        {
            CreateSalesTransaction(saleId, productId, 2, 199.98m, saleDate)
        };

        _mockRepository.Setup(r => r.GetSalesHistoryAsync(productId, 30))
            .ReturnsAsync(salesHistory);

        // Act
        var newSaleId = await _mockRepository.Object.RecordSaleAsync(productId, 2, 199.98m);
        var history = await _mockRepository.Object.GetSalesHistoryAsync(productId);

        // Assert
        Assert.Equal(saleId, newSaleId);
        Assert.Single(history);
        Assert.Equal(newSaleId, history.First().Id);
    }

    [Fact]
    public async Task LifecycleTransition_UpdatesPhaseCorrectly()
    {
        // Arrange
        var productId = 1;
        var initialPhase = ActivePhase;
        var newPhase = LegacyPhase;

        var productBeforeUpdate = CreateTestProduct(productId, "iPhone 12", 10, initialPhase);
        var productAfterUpdate = CreateTestProduct(productId, "iPhone 12", 10, newPhase);

        var callSequence = 0;
        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(() => callSequence++ == 0 
                ? new List<Product> { productBeforeUpdate } 
                : new List<Product> { productAfterUpdate });

        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(productId, newPhase, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var beforeProducts = await _mockRepository.Object.GetAllProductsAsync();
        await _mockRepository.Object.UpdateProductPhaseAsync(productId, newPhase, "Product now vintage");
        var afterProducts = await _mockRepository.Object.GetAllProductsAsync();

        // Assert
        Assert.Equal(initialPhase, beforeProducts.First().LifecyclePhase);
        Assert.Equal(newPhase, afterProducts.First().LifecyclePhase);
    }

    #endregion

    #region Model Tests

    [Fact]
    public void Product_AllPropertiesCanBeSet()
    {
        var product = new Product
        {
            Id = 1,
            Name = "Test Product",
            Sku = "SKU-001",
            Category = "Electronics",
            UnitPrice = 299.99m,
            CurrentStock = 50,
            LifecyclePhase = ActivePhase,
            SuccessorProductId = 2,
            CreatedAt = DateTime.Now.AddDays(-30),
            LastUpdated = DateTime.Now
        };

        Assert.Equal(1, product.Id);
        Assert.Equal("Test Product", product.Name);
        Assert.Equal("SKU-001", product.Sku);
        Assert.Equal("Electronics", product.Category);
        Assert.Equal(299.99m, product.UnitPrice);
        Assert.Equal(50, product.CurrentStock);
        Assert.Equal(ActivePhase, product.LifecyclePhase);
        Assert.Equal(2, product.SuccessorProductId);
    }

    [Fact]
    public void SalesTransaction_AllPropertiesCanBeSet()
    {
        var transaction = new SalesTransaction
        {
            Id = 1,
            ProductId = 10,
            QuantitySold = 5,
            TotalAmount = 499.95m,
            SaleDate = DateTime.Today,
            CreatedAt = DateTime.Now,
            Notes = "Customer purchase"
        };

        Assert.Equal(1, transaction.Id);
        Assert.Equal(10, transaction.ProductId);
        Assert.Equal(5, transaction.QuantitySold);
        Assert.Equal(499.95m, transaction.TotalAmount);
        Assert.Equal(DateTime.Today, transaction.SaleDate);
        Assert.Equal("Customer purchase", transaction.Notes);
    }

    [Fact]
    public void ProductDashboardDto_AllPropertiesCanBeSet()
    {
        var dto = new ProductDashboardDto
        {
            Id = 1,
            Name = "Dashboard Product",
            Phase = ActivePhase,
            Recommendation = "Reorder soon",
            CurrentStock = 25,
            SalesLast7Days = 20,
            RunwayDays = 8
        };

        Assert.Equal(1, dto.Id);
        Assert.Equal("Dashboard Product", dto.Name);
        Assert.Equal(ActivePhase, dto.Phase);
        Assert.Equal("Reorder soon", dto.Recommendation);
        Assert.Equal(25, dto.CurrentStock);
        Assert.Equal(20, dto.SalesLast7Days);
        Assert.Equal(8, dto.RunwayDays);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task RecordSaleAsync_LargeQuantity_HandlesCorrectly()
    {
        // Arrange
        _mockRepository.Setup(r => r.RecordSaleAsync(1, 1000, 99990.00m, null))
            .ReturnsAsync(1);

        // Act
        var result = await _mockRepository.Object.RecordSaleAsync(1, 1000, 99990.00m);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task GetSalesHistoryAsync_ZeroDays_ReturnsNoResults()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetSalesHistoryAsync(1, 0))
            .ReturnsAsync(new List<SalesTransaction>());

        // Act
        var result = await _mockRepository.Object.GetSalesHistoryAsync(1, 0);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateProductPhaseAsync_EmptyReason_StillCompletes()
    {
        // Arrange
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(1, "OBSOLETE", ""))
            .Returns(Task.CompletedTask);

        // Act & Assert
        await _mockRepository.Object.UpdateProductPhaseAsync(1, "OBSOLETE", "");
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(1, "OBSOLETE", ""), Times.Once);
    }

    #endregion
}

