using System.ComponentModel.DataAnnotations;

namespace TechnologyStoreAutomation.backend.trendCalculator.data;

/// <summary>
/// Core Product entity with validation attributes
/// </summary>
public class Product
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Product name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Product name must be between 1 and 200 characters")]
    public required string Name { get; set; }
    
    [Required(ErrorMessage = "SKU is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "SKU must be between 1 and 50 characters")]
    public required string Sku { get; set; }
    
    [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters")]
    public string? Category { get; set; }
    
    [Range(0.01, 999999.99, ErrorMessage = "Unit price must be between $0.01 and $999,999.99")]
    public decimal UnitPrice { get; set; }
    
    [Range(0, int.MaxValue, ErrorMessage = "Current stock cannot be negative")]
    public int CurrentStock { get; set; }
    
    [RegularExpression("^(ACTIVE|LEGACY|OBSOLETE)$", ErrorMessage = "Lifecycle phase must be ACTIVE, LEGACY, or OBSOLETE")]
    public string LifecyclePhase { get; set; } = "ACTIVE";
    
    public int? SuccessorProductId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}