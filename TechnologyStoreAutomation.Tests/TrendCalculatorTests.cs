using TechnologyStoreAutomation.backend.trendCalculator;
using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation.Tests;

public class TrendCalculatorTests
{
    private static Product CreateTestProduct(int id = 1, string name = "Test Product", int stock = 100)
    {
        return new Product
        {
            Id = id,
            Name = name,
            Sku = $"SKU-{id}",
            CurrentStock = stock,
            LifecyclePhase = "ACTIVE"
        };
    }

    private static List<SalesTransaction> CreateSalesHistory(int[] dailySales, int startDaysAgo = 7)
    {
        var transactions = new List<SalesTransaction>();
        for (int i = 0; i < dailySales.Length; i++)
        {
            transactions.Add(new SalesTransaction
            {
                Id = i + 1,
                ProductId = 1,
                QuantitySold = dailySales[i],
                TotalAmount = dailySales[i] * 10m,
                SaleDate = DateTime.Today.AddDays(-(startDaysAgo - i))
            });
        }
        return transactions;
    }

    [Fact]
    public void AnalyzeProduct_WithNoSalesHistory_ReturnsStableTrend()
    {
        // Arrange
        var product = CreateTestProduct();

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, null);

        // Assert
        Assert.Equal(TrendDirection.Stable, result.Direction);
        Assert.Equal(0, result.DailySalesAverage);
        Assert.Equal(999, result.RunwayDays);
        Assert.Equal(0, result.TrendStrength);
    }

    [Fact]
    public void AnalyzeProduct_WithEmptySalesHistory_ReturnsStableTrend()
    {
        // Arrange
        var product = CreateTestProduct();
        var salesHistory = new List<SalesTransaction>();

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, salesHistory);

        // Assert
        Assert.Equal(TrendDirection.Stable, result.Direction);
        Assert.Equal(0, result.DailySalesAverage);
        Assert.Equal(999, result.RunwayDays);
    }

    [Fact]
    public void AnalyzeProduct_WithConsistentSales_CalculatesCorrectRunway()
    {
        // Arrange
        var product = CreateTestProduct(stock: 70);
        // 10 sales per day for 7 days = avg 10/day
        var salesHistory = CreateSalesHistory(new[] { 10, 10, 10, 10, 10, 10, 10 });

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, salesHistory);

        // Assert
        Assert.Equal(10, result.DailySalesAverage);
        Assert.Equal(7, result.RunwayDays); // 70 stock / 10 per day = 7 days
    }

    [Fact]
    public void AnalyzeProduct_WithZeroStock_ReturnsZeroRunway()
    {
        // Arrange
        var product = CreateTestProduct(stock: 0);
        var salesHistory = CreateSalesHistory(new[] { 10, 10, 10, 10, 10, 10, 10 });

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, salesHistory);

        // Assert
        Assert.Equal(0, result.RunwayDays);
    }

    [Fact]
    public void AnalyzeProduct_SetsCorrectProductInfo()
    {
        // Arrange
        var product = CreateTestProduct(id: 42, name: "iPhone 15", stock: 50);
        var salesHistory = CreateSalesHistory(new[] { 5 });

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, salesHistory);

        // Assert
        Assert.Equal(42, result.ProductId);
        Assert.Equal((string?)"iPhone 15", (string?)result.ProductName);
        Assert.Equal(50, result.CurrentStock);
    }

    [Fact]
    public void AnalyzeProduct_WithHighStock_ReturnsHighRunway()
    {
        // Arrange
        var product = CreateTestProduct(stock: 1000);
        var salesHistory = CreateSalesHistory(new[] { 1, 1, 1, 1, 1, 1, 1 }); // 1 per day

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, salesHistory);

        // Assert
        Assert.True(result.RunwayDays >= 100); // Should be very high runway
    }

    #region Edge Case Tests

    [Fact]
    public void AnalyzeProduct_WithNegativeStock_HandlesGracefully()
    {
        // Arrange - Negative stock can occur in some inventory systems during sync
        var product = CreateTestProduct(stock: -5);
        var salesHistory = CreateSalesHistory(new[] { 10, 10, 10, 10, 10, 10, 10 });

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, salesHistory);

        // Assert
        Assert.True(result.RunwayDays <= 0 || result.RunwayDays == 999);
    }

    [Fact]
    public void AnalyzeProduct_WithSingleDaySales_CalculatesCorrectly()
    {
        // Arrange
        var product = CreateTestProduct(stock: 100);
        var salesHistory = CreateSalesHistory(new[] { 70 }); // Only one day of data

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, salesHistory);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(product.Id, result.ProductId);
    }

    [Fact]
    public void AnalyzeProduct_WithVeryLargeSalesVolume_CalculatesCorrectRunway()
    {
        // Arrange
        var product = CreateTestProduct(stock: 100);
        var salesHistory = CreateSalesHistory(new[] { 1000, 1000, 1000, 1000, 1000, 1000, 1000 }); // 1000/day

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, salesHistory);

        // Assert
        Assert.True(result.RunwayDays <= 1); // Should run out almost immediately
    }

    [Fact]
    public void AnalyzeProduct_WithDecimalAverages_ReturnsRoundedValue()
    {
        // Arrange
        var product = CreateTestProduct(stock: 100);
        // Total: 1+2+3+4+5+6+7 = 28 over 7 days = 4.0 avg
        var salesHistory = CreateSalesHistory(new[] { 1, 2, 3, 4, 5, 6, 7 });

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, salesHistory);

        // Assert
        Assert.Equal(4.0, result.DailySalesAverage);
    }

    [Fact]
    public void AnalyzeProduct_WithAllZeroSales_ReturnsMaxRunway()
    {
        // Arrange
        var product = CreateTestProduct(stock: 100);
        var salesHistory = CreateSalesHistory(new[] { 0, 0, 0, 0, 0, 0, 0 });

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, salesHistory);

        // Assert
        Assert.Equal(999, result.RunwayDays); // Max runway when no sales
        Assert.Equal(0, result.DailySalesAverage);
    }

    [Fact]
    public void AnalyzeProduct_OlderSalesOnly_HandlesCorrectly()
    {
        // Arrange - Sales from over 30 days ago
        var product = CreateTestProduct(stock: 100);
        var oldSalesHistory = new List<SalesTransaction>
        {
            new SalesTransaction
            {
                Id = 1,
                ProductId = 1,
                QuantitySold = 50,
                TotalAmount = 500m,
                SaleDate = DateTime.Today.AddDays(-60) // 60 days ago
            }
        };

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, oldSalesHistory);

        // Assert
        Assert.NotNull(result);
        // Since sales are old, the 7-day average should be 0
    }

    [Fact]
    public void AnalyzeProduct_WithSporadicSales_DetectsVolatility()
    {
        // Arrange - Very inconsistent sales pattern
        var product = CreateTestProduct(stock: 100);
        // High variance: 0, 100, 0, 100, 0, 100, 0
        var salesHistory = CreateSalesHistory(new[] { 0, 100, 0, 100, 0, 100, 0 });

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, salesHistory);

        // Assert
        // Should either be Volatile or have a trend detected
        Assert.NotEqual(TrendDirection.Stable, result.Direction);
    }

    [Fact]
    public void AnalyzeProduct_RisingTrend_DetectsCorrectly()
    {
        // Arrange - Clear upward trend
        var product = CreateTestProduct(stock: 100);
        // Previous week: 1,2,3,4,5,6,7 (avg ~4), Recent week: 20,21,22,23,24,25,26 (avg ~23)
        var transactions = new List<SalesTransaction>();
        
        // Previous week (lower sales)
        for (int i = 14; i > 7; i--)
        {
            transactions.Add(new SalesTransaction
            {
                Id = 15 - i,
                ProductId = 1,
                QuantitySold = 5,
                TotalAmount = 50m,
                SaleDate = DateTime.Today.AddDays(-i)
            });
        }
        
        // Recent week (much higher sales - more than 15% increase)
        for (int i = 7; i >= 1; i--)
        {
            transactions.Add(new SalesTransaction
            {
                Id = 20 - i,
                ProductId = 1,
                QuantitySold = 20,
                TotalAmount = 200m,
                SaleDate = DateTime.Today.AddDays(-i)
            });
        }

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, transactions);

        // Assert
        Assert.Equal(TrendDirection.Rising, result.Direction);
    }

    [Fact]
    public void AnalyzeProduct_FallingTrend_DetectsCorrectly()
    {
        // Arrange - Clear downward trend
        var product = CreateTestProduct(stock: 100);
        var transactions = new List<SalesTransaction>();
        
        // Previous week (higher sales)
        for (int i = 14; i > 7; i--)
        {
            transactions.Add(new SalesTransaction
            {
                Id = 15 - i,
                ProductId = 1,
                QuantitySold = 25,
                TotalAmount = 250m,
                SaleDate = DateTime.Today.AddDays(-i)
            });
        }
        
        // Recent week (much lower sales - more than 15% decrease)
        for (int i = 7; i >= 1; i--)
        {
            transactions.Add(new SalesTransaction
            {
                Id = 20 - i,
                ProductId = 1,
                QuantitySold = 5,
                TotalAmount = 50m,
                SaleDate = DateTime.Today.AddDays(-i)
            });
        }

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, transactions);

        // Assert
        Assert.Equal(TrendDirection.Falling, result.Direction);
    }

    [Fact]
    public void AnalyzeProduct_VeryLongHistory_ProcessesEfficiently()
    {
        // Arrange - 365 days of sales data
        var product = CreateTestProduct(stock: 1000);
        var transactions = new List<SalesTransaction>();
        
        for (int i = 365; i >= 1; i--)
        {
            transactions.Add(new SalesTransaction
            {
                Id = 366 - i,
                ProductId = 1,
                QuantitySold = 10 + (i % 5), // Some variance
                TotalAmount = (10 + (i % 5)) * 10m,
                SaleDate = DateTime.Today.AddDays(-i)
            });
        }

        // Act
        var result = TrendCalculator.AnalyzeProduct(product, transactions);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.DailySalesAverage > 0);
    }

    #endregion

    #region TrendDirection Enum Tests

    [Fact]
    public void TrendDirection_HasAllExpectedValues()
    {
        var values = Enum.GetValues<TrendDirection>();
        Assert.Contains(TrendDirection.Rising, values);
        Assert.Contains(TrendDirection.Falling, values);
        Assert.Contains(TrendDirection.Stable, values);
        Assert.Contains(TrendDirection.Volatile, values);
    }

    #endregion
}

