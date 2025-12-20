using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Interfaces;

namespace TechnologyStore.Shared.Services;

/// <summary>
/// Core intelligence engine that analyzes sales trends and predicts inventory needs.
/// Implements ITrendCalculator for dependency injection and testing.
/// </summary>
public class TrendCalculatorService : ITrendCalculator
{
    /// <summary>
    /// Analyzes a product's sales history and generates trend insights
    /// </summary>
    public TrendAnalysis AnalyzeProduct(Product product, IEnumerable<SalesTransaction>? salesHistory)
    {
        var salesList = salesHistory?.ToList() ?? new List<SalesTransaction>();

        if (!salesList.Any())
        {
            return new TrendAnalysis
            {
                ProductId = product.Id,
                ProductName = product.Name,
                CurrentStock = product.CurrentStock,
                DailySalesAverage = 0,
                RunwayDays = 999,
                Direction = TrendDirection.Stable,
                TrendStrength = 0
            };
        }

        // Sort by date ascending
        var sortedSales = salesList.OrderBy(s => s.SaleDate).ToList();

        // Calculate 7-day moving average
        var last7Days = sortedSales.Where(s => s.SaleDate >= DateTime.Today.AddDays(-7)).ToList();
        double dailyAverage = last7Days.Any()
            ? last7Days.Sum(s => s.QuantitySold) / 7.0
            : 0;

        // Calculate runway days (how long until stock runs out)
        int runwayDays = dailyAverage > 0
            ? (int)Math.Ceiling(product.CurrentStock / dailyAverage)
            : 999;

        // Determine trend direction by comparing recent vs older sales
        var trendDirection = CalculateTrendDirection(sortedSales);
        var trendStrength = CalculateTrendStrength(sortedSales);
        var isAccelerating = DetectAcceleration(sortedSales);

        return new TrendAnalysis
        {
            ProductId = product.Id,
            ProductName = product.Name,
            CurrentStock = product.CurrentStock,
            DailySalesAverage = Math.Round(dailyAverage, 2),
            RunwayDays = runwayDays,
            Direction = trendDirection,
            TrendStrength = Math.Round(trendStrength, 2),
            IsAccelerating = isAccelerating
        };
    }

    /// <summary>
    /// Quick method to calculate just the runway days without full analysis
    /// </summary>
    public static int CalculateRunwayDays(int currentStock, double dailySalesAverage)
    {
        if (dailySalesAverage <= 0) return 999;
        return (int)Math.Ceiling(currentStock / dailySalesAverage);
    }

    /// <summary>
    /// Calculates trend direction by comparing recent week vs previous week
    /// </summary>
    private static TrendDirection CalculateTrendDirection(List<SalesTransaction> sales)
    {
        if (sales.Count < 7) return TrendDirection.Stable;

        var recentWeek = sales.Where(s => s.SaleDate >= DateTime.Today.AddDays(-7))
            .Sum(s => s.QuantitySold);

        var previousWeek = sales.Where(s => s.SaleDate >= DateTime.Today.AddDays(-14) && s.SaleDate < DateTime.Today.AddDays(-7))
            .Sum(s => s.QuantitySold);

        if (previousWeek == 0) return TrendDirection.Stable;

        double changePercent = ((double)(recentWeek - previousWeek) / previousWeek) * 100;

        // Check for volatility (high variance) - ensure dailyCounts is List<int>
        var dailyCounts = sales.GroupBy(s => s.SaleDate)
            .Select(g => g.Sum(s => s.QuantitySold))
            .ToList();

        double variance = CalculateVariance(dailyCounts);
        if (variance > 50)
            return TrendDirection.Volatile;

        if (changePercent > 15) return TrendDirection.Rising;
        if (changePercent < -15) return TrendDirection.Falling;

        return TrendDirection.Stable;
    }

    /// <summary>
    /// Calculates trend strength (-1 to 1, negative = declining, positive = growing)
    /// </summary>
    private static double CalculateTrendStrength(List<SalesTransaction> sales)
    {
        if (sales.Count < 7) return 0;

        var recentWeek = sales.Where(s => s.SaleDate >= DateTime.Today.AddDays(-7))
            .Sum(s => s.QuantitySold);

        var previousWeek = sales.Where(s => s.SaleDate >= DateTime.Today.AddDays(-14) && s.SaleDate < DateTime.Today.AddDays(-7))
            .Sum(s => s.QuantitySold);

        if (previousWeek == 0) return recentWeek > 0 ? 1.0 : 0;

        double changeRatio = (double)(recentWeek - previousWeek) / previousWeek;

        // Clamp between -1 and 1
        return Math.Max(-1.0, Math.Min(1.0, changeRatio));
    }

    /// <summary>
    /// Detects if sales are accelerating (trend is getting stronger)
    /// </summary>
    private static bool DetectAcceleration(List<SalesTransaction> sales)
    {
        if (sales.Count < 21) return false;

        var week1 = sales.Where(s => s.SaleDate >= DateTime.Today.AddDays(-7)).Sum(s => s.QuantitySold);
        var week2 = sales.Where(s => s.SaleDate >= DateTime.Today.AddDays(-14) && s.SaleDate < DateTime.Today.AddDays(-7)).Sum(s => s.QuantitySold);
        var week3 = sales.Where(s => s.SaleDate >= DateTime.Today.AddDays(-21) && s.SaleDate < DateTime.Today.AddDays(-14)).Sum(s => s.QuantitySold);

        // Acceleration means each week is consistently higher than the previous
        return (week1 > week2) && (week2 > week3);
    }

    /// <summary>
    /// Calculates statistical variance of a dataset
    /// </summary>
    private static double CalculateVariance(List<int> values)
    {
        if (!values.Any()) return 0;

        double average = values.Average();
        double sumOfSquares = values.Sum(v => Math.Pow(v - average, 2));

        return sumOfSquares / values.Count;
    }
}
