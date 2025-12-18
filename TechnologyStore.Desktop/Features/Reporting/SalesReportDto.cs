namespace TechnologyStore.Desktop.Features.Reporting;

/// <summary>
/// Data transfer object for sales reports
/// </summary>
public class SalesReportDto
{
    public string ReportType { get; set; } = string.Empty; // Daily, Weekly, Monthly
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalTransactions { get; set; }
    public int TotalUnitsSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageSaleAmount { get; set; }
    public List<ProductSalesBreakdown> ProductBreakdown { get; set; } = new();
}

/// <summary>
/// Sales breakdown by product
/// </summary>
public class ProductSalesBreakdown
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public decimal PercentageOfTotal { get; set; }
}
