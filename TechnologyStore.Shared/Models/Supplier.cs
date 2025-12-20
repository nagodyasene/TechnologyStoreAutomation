using System.ComponentModel.DataAnnotations;

namespace TechnologyStore.Shared.Models;

/// <summary>
/// Represents a supplier that provides products to the store
/// </summary>
public class Supplier
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Supplier name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Supplier name must be between 1 and 200 characters")]
    public required string Name { get; set; }
    
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [StringLength(255)]
    public required string Email { get; set; }
    
    [Phone(ErrorMessage = "Invalid phone number")]
    [StringLength(50)]
    public string? Phone { get; set; }
    
    [StringLength(100)]
    public string? ContactPerson { get; set; }
    
    [StringLength(500)]
    public string? Address { get; set; }
    
    /// <summary>
    /// Average lead time in days for this supplier to deliver orders
    /// </summary>
    public int LeadTimeDays { get; set; } = 7;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
