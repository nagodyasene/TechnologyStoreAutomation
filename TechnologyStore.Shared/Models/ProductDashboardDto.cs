namespace TechnologyStore.Shared.Models;

// DTO for our Dashboard Grid
public class ProductDashboardDto
{
    public int Id { get; init; }

    public required string Name { get; init; }
    public string? Category { get; init; }
    public required string Phase { get; init; } // Active, legacy or obsolete
    public required string Recommendation { get; init; }

    public int CurrentStock { get; init; }
    public int SalesLast7Days { get; init; }
    public int RunwayDays { get; init; }
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}
