using System.ComponentModel.DataAnnotations;

namespace TechnologyStore.Desktop.Features.Products.Data;

/// <summary>
/// Represents a single sales transaction with validation
/// </summary>
public class SalesTransaction
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Product ID is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Product ID must be a positive integer")]
    public int ProductId { get; set; }
    
    [Required(ErrorMessage = "Quantity sold is required")]
    [Range(1, 10000, ErrorMessage = "Quantity must be between 1 and 10,000")]
    public int QuantitySold { get; set; }
    
    [Required(ErrorMessage = "Total amount is required")]
    [Range(0.01, 9999999.99, ErrorMessage = "Total amount must be between $0.01 and $9,999,999.99")]
    public decimal TotalAmount { get; set; }
    
    [Required(ErrorMessage = "Sale date is required")]
    public DateTime SaleDate { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }
}

