using TechnologyStore.Desktop.Services;
using System.Data;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TechnologyStore.Desktop.Features.Reporting;

/// <summary>
/// Service for generating sales reports
/// </summary>
public class SalesReportService : ISalesReportService
{
    private readonly string _connectionString;
    private readonly ILogger<SalesReportService> _logger;

    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    public SalesReportService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        _connectionString = connectionString;
        _logger = AppLogger.CreateLogger<SalesReportService>();
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// Executes a database operation with retry logic for transient failures
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<IDbConnection, Task<T>> operation)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var connection = CreateConnection();
                return await operation(connection);
            }
            catch (NpgsqlException ex) when (IsTransientError(ex) && attempt < MaxRetries)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Transient database error on attempt {Attempt}, retrying...", attempt);
                await Task.Delay(RetryDelay * attempt);
            }
        }

        throw lastException!;
    }

    private static bool IsTransientError(NpgsqlException ex)
    {
        return ex.SqlState is "08000" or "08003" or "08006" or "40001" or "40P01";
    }

    public async Task<SalesReportDto> GetDailyReportAsync(DateTime date)
    {
        return await GetCustomRangeReportAsync(date.Date, date.Date);
    }

    public async Task<SalesReportDto> GetWeeklyReportAsync(DateTime weekStart)
    {
        var start = weekStart.Date;
        var end = start.AddDays(6);
        var report = await GetCustomRangeReportAsync(start, end);
        report.ReportType = "Weekly";
        return report;
    }

    public async Task<SalesReportDto> GetMonthlyReportAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Local);
        var end = start.AddMonths(1).AddDays(-1);
        var report = await GetCustomRangeReportAsync(start, end);
        report.ReportType = "Monthly";
        return report;
    }

    public async Task<SalesReportDto> GetCustomRangeReportAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException("Start date cannot be later than end date.", nameof(startDate));
        }

        _logger.LogInformation("Generating sales report for {StartDate} to {EndDate}", startDate, endDate);

        const string summarySql = @"
            SELECT 
                COUNT(*) as TotalTransactions,
                COALESCE(SUM(quantity_sold), 0) as TotalUnitsSold,
                COALESCE(SUM(total_amount), 0) as TotalRevenue
            FROM sales_transactions
            WHERE sale_date >= @StartDate AND sale_date <= @EndDate";

        const string breakdownSql = @"
            SELECT 
                p.id as ProductId,
                p.name as ProductName,
                COALESCE(SUM(st.quantity_sold), 0) as UnitsSold,
                COALESCE(SUM(st.total_amount), 0) as Revenue
            FROM products p
            LEFT JOIN sales_transactions st ON p.id = st.product_id 
                AND st.sale_date >= @StartDate AND st.sale_date <= @EndDate
            GROUP BY p.id, p.name
            HAVING SUM(st.quantity_sold) > 0
            ORDER BY Revenue DESC";

        return await ExecuteWithRetryAsync(async connection =>
        {
            // Get summary
            var summary = await connection.QueryFirstAsync<SalesSummaryDto>(summarySql, new { StartDate = startDate, EndDate = endDate });

            // Get product breakdown
            var breakdown = await connection.QueryAsync<ProductSalesBreakdown>(breakdownSql, new { StartDate = startDate, EndDate = endDate });
            var breakdownList = breakdown.ToList();

            // Calculate percentages
            var totalRevenue = summary.TotalRevenue;
            foreach (var item in breakdownList)
            {
                item.PercentageOfTotal = totalRevenue > 0
                    ? Math.Round((item.Revenue / totalRevenue) * 100, 2)
                    : 0;
            }

            var reportType = startDate == endDate ? "Daily" : "Custom";

            return new SalesReportDto
            {
                ReportType = reportType,
                StartDate = startDate,
                EndDate = endDate,
                TotalTransactions = (int)summary.TotalTransactions,
                TotalUnitsSold = (int)summary.TotalUnitsSold,
                TotalRevenue = totalRevenue,
                AverageSaleAmount = summary.TotalTransactions > 0
                    ? Math.Round(totalRevenue / summary.TotalTransactions, 2)
                    : 0,
                ProductBreakdown = breakdownList
            };
        });
    }

    public string ExportToCsv(SalesReportDto report)
    {
        var sb = new StringBuilder();

        // Header info
        sb.AppendLine($"Sales Report - {report.ReportType}");
        sb.AppendLine($"Period: {report.StartDate:yyyy-MM-dd} to {report.EndDate:yyyy-MM-dd}");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Summary
        sb.AppendLine("SUMMARY");
        sb.AppendLine($"Total Transactions,{report.TotalTransactions}");
        sb.AppendLine($"Total Units Sold,{report.TotalUnitsSold}");
        sb.AppendLine($"Total Revenue,{report.TotalRevenue:C}");
        sb.AppendLine($"Average Sale Amount,{report.AverageSaleAmount:C}");
        sb.AppendLine();

        // Product breakdown
        sb.AppendLine("PRODUCT BREAKDOWN");
        sb.AppendLine("Product,Units Sold,Revenue,% of Total");

        foreach (var item in report.ProductBreakdown)
        {
            sb.AppendLine($"\"{item.ProductName}\",{item.UnitsSold},{item.Revenue:F2},{item.PercentageOfTotal}%");
        }

        _logger.LogInformation("Exported {ReportType} report to CSV", report.ReportType);
        return sb.ToString();
    }

    private class SalesSummaryDto
    {
        public long TotalTransactions { get; set; }
        public long TotalUnitsSold { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
