namespace TechnologyStore.Desktop.Features.Leave;

/// <summary>
/// Represents a leave/vacation request
/// </summary>
public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public LeaveType LeaveType { get; set; } = LeaveType.Annual;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public int? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    // Navigation properties (not populated by default)
    public string? EmployeeName { get; set; }
    public string? ReviewerName { get; set; }
}

/// <summary>
/// Types of leave that can be requested
/// </summary>
public enum LeaveType
{
    Annual,
    Sick,
    Personal,
    Unpaid
}

/// <summary>
/// Status of a leave request
/// </summary>
public enum LeaveStatus
{
    Pending,
    Approved,
    Rejected
}
