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
}

