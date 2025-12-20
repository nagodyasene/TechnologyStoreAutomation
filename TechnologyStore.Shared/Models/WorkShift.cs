using System;

namespace TechnologyStore.Shared.Models;

/// <summary>
/// Represents a scheduled work shift for an employee.
/// </summary>
public class WorkShift
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Scheduled;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    
    // Navigation property placeholder (optional, depending on ORM usage)
    public string? EmployeeName { get; set; } 
}

public enum ShiftStatus
{
    Scheduled,
    Completed,
    Absent,
    Late
}
