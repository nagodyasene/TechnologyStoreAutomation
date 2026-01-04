namespace TechnologyStore.Desktop.Features.HR;

public sealed class EmployeeManagementRow
{
    public int EmployeeId { get; set; }
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleText { get; set; } = "EMPLOYEE";
    public bool IsActive { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime HireDate { get; set; }
    public int RemainingLeaveDays { get; set; }
    public decimal HourlyRate { get; set; }
}


