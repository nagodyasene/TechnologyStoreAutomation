namespace TechnologyStoreAutomation.backend.trendCalculator.data;

/// <summary>
/// Core Product entity
/// </summary>
public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Sku { get; set; }
    public string? Category { get; set; }
    public decimal UnitPrice { get; set; }
    public int CurrentStock { get; set; }
    public string LifecyclePhase { get; set; } = "ACTIVE"; // ACTIVE, LEGACY, OBSOLETE
    public int? SuccessorProductId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}