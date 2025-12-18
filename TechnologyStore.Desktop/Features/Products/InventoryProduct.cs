namespace TechnologyStore.Desktop.Features.Products;

public class InventoryProduct
{
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public int Stock { get; set; }
    public decimal UnitPrice { get; set; }
    public double DailyVelocity { get; set; } // Calculated from SQL
    public DateTime? SupportEndDate { get; set; }
    public bool SuccessorAnnounced { get; set; }
    public LifecyclePhase CurrentPhase { get; set; }
    
    public InventoryProduct(string sku, string name)
    {
        Sku = sku;
        Name = name;
    }

    [System.Text.Json.Serialization.JsonConstructor]
    public InventoryProduct()
    {
        Sku = string.Empty;
        Name = string.Empty;
    }
}