namespace TechnologyStoreAutomation.backend.visitorCountPrediction;

/// <summary>
/// Represents estimated daily store traffic based on sales data
/// </summary>
public class DailyTraffic
{
    public DateTime Date { get; set; }
    public int EstimatedVisitors { get; set; }
    public int TotalTransactions { get; set; }
    public int TotalUnitsSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public double ConversionRate { get; set; } // Estimated visitors who made a purchase
}
