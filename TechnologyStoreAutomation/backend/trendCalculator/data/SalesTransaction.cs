namespace TechnologyStoreAutomation.backend.trendCalculator.data;

/// <summary>
/// Represents a single sales transaction
/// </summary>
public class SalesTransaction
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int QuantitySold { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime SaleDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

