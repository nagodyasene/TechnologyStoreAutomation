using System;

namespace TechnologyStore.Shared.Models;

/// <summary>
/// Represents a single time tracking event (Clock In, Out, etc.)
/// </summary>
public class TimeEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public TimeEntryType EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Notes { get; set; }
    public bool IsManualEntry { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum TimeEntryType
{
    ClockIn,
    ClockOut,
    StartLunch,
    EndLunch
}
