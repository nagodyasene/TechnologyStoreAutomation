namespace TechnologyStore.Desktop.Features.Leave;

/// <summary>
/// Represents an employee linked to a user account
/// </summary>
public class Employee
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime HireDate { get; set; }
    public int RemainingLeaveDays { get; set; } = 14;
    public decimal HourlyRate { get; set; } = 15.00m;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property (not populated by default)
    public string? FullName { get; set; }
}
