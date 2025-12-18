namespace TechnologyStoreAutomation.backend.reporting;

/// <summary>
/// Interface for sales report generation
/// </summary>
public interface ISalesReportService
{
    /// <summary>
    /// Gets a sales report for a specific date
    /// </summary>
    Task<SalesReportDto> GetDailyReportAsync(DateTime date);

    /// <summary>
    /// Gets a sales report for a week starting on the given date
    /// </summary>
    Task<SalesReportDto> GetWeeklyReportAsync(DateTime weekStart);

    /// <summary>
    /// Gets a sales report for a specific month
    /// </summary>
    Task<SalesReportDto> GetMonthlyReportAsync(int year, int month);

    /// <summary>
    /// Gets a sales report for a custom date range
    /// </summary>
    Task<SalesReportDto> GetCustomRangeReportAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Exports a report to CSV format
    /// </summary>
    string ExportToCsv(SalesReportDto report);
}
