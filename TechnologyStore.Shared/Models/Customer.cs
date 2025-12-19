namespace TechnologyStore.Shared.Models;

/// <summary>
/// Represents a customer account (registered or guest)
/// </summary>
public class Customer
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public string? PasswordHash { get; set; }
    public required string FullName { get; set; }
    public string? Phone { get; set; }
    public bool IsGuest { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }
}
