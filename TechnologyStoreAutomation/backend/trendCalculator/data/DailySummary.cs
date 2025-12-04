namespace TechnologyStoreAutomation.backend.trendCalculator.data;

/// <summary>
/// Daily summary snapshot for fast dashboard queries
/// </summary>
public class DailySummary
{
    public int Id { get; set; }
    public DateTime SummaryDate { get; set; }
    public int ProductId { get; set; }
    public int ClosingStock { get; set; }
    public int TotalSold { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}