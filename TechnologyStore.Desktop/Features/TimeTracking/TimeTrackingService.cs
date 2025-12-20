using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TechnologyStore.Desktop.Services;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Desktop.Features.TimeTracking;

public class TimeTrackingService : ITimeTrackingService
{
    private readonly ITimeTrackingRepository _repository;
    private readonly ILogger<TimeTrackingService> _logger;

    public TimeTrackingService(ITimeTrackingRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = AppLogger.CreateLogger<TimeTrackingService>();
    }

    public async Task<TimeEntry> ClockInAsync(int userId)
    {
        var lastEvent = await _repository.GetLastEventAsync(userId);
        if (lastEvent != null && lastEvent.EventType != TimeEntryType.ClockOut && lastEvent.Timestamp.Date == DateTime.Today)
        {
            // Allow re-clocking in only if the last event was clock out.
            // However, maybe they forgot to clock out yesterday?
            // For now, strict validation: simple state machine.
            // If they are currently working or on lunch, they can't clock in.

            // Actually, simple rule: If last event is ClockIn or EndLunch, they are Working.
            // If StartLunch, they are OnLunch.
            // If ClockOut or null, they are NotWorking.
            throw new InvalidOperationException("You are already clocked in for today.");
        }

        var entry = new TimeEntry
        {
            UserId = userId,
            EventType = TimeEntryType.ClockIn,
            Timestamp = DateTime.Now
        };

        _logger.LogInformation("User {UserId} clocking in at {Time}", userId, entry.Timestamp);
        return await _repository.LogEventAsync(entry);
    }

    public async Task<TimeEntry> ClockOutAsync(int userId)
    {
        var lastEvent = await _repository.GetLastEventAsync(userId);
        if (lastEvent == null || lastEvent.EventType == TimeEntryType.ClockOut)
        {
            throw new InvalidOperationException("You are not currently clocked in.");
        }

        var entry = new TimeEntry
        {
            UserId = userId,
            EventType = TimeEntryType.ClockOut,
            Timestamp = DateTime.Now
        };

        _logger.LogInformation("User {UserId} clocking out at {Time}", userId, entry.Timestamp);
        return await _repository.LogEventAsync(entry);
    }

    public async Task<TimeEntry> StartLunchAsync(int userId)
    {
        var lastEvent = await _repository.GetLastEventAsync(userId);
        if (lastEvent == null || (lastEvent.EventType != TimeEntryType.ClockIn && lastEvent.EventType != TimeEntryType.EndLunch))
        {
            throw new InvalidOperationException("You must be clocked in (and not on lunch) to start lunch.");
        }

        var entry = new TimeEntry
        {
            UserId = userId,
            EventType = TimeEntryType.StartLunch,
            Timestamp = DateTime.Now
        };

        return await _repository.LogEventAsync(entry);
    }

    public async Task<TimeEntry> EndLunchAsync(int userId)
    {
        var lastEvent = await _repository.GetLastEventAsync(userId);
        if (lastEvent == null || lastEvent.EventType != TimeEntryType.StartLunch)
        {
            throw new InvalidOperationException("You must be on lunch to end lunch.");
        }

        var entry = new TimeEntry
        {
            UserId = userId,
            EventType = TimeEntryType.EndLunch,
            Timestamp = DateTime.Now
        };

        return await _repository.LogEventAsync(entry);
    }

    public async Task<TimeEntry?> GetCurrentStatusAsync(int userId)
    {
        return await _repository.GetLastEventAsync(userId);
    }

    public async Task<TimeSpan> CalculateDailyHoursAsync(int userId, DateTime date)
    {
        var events = await _repository.GetDailyEventsAsync(userId, date);
        var entries = events.OrderBy(e => e.Timestamp).ToList();

        // Pass DateTime.Now as 'now' reference for stable calculation of ongoing work
        return CalculateHoursFromEntries(entries, date.Date == DateTime.Today ? DateTime.Now : null);
    }

    private static TimeSpan CalculateHoursFromEntries(List<TimeEntry> entries, DateTime? currentWorkEndTime)
    {
        TimeSpan totalWork = TimeSpan.Zero;
        DateTime? workStart = null;
        DateTime? lunchStart = null;

        foreach (var entry in entries)
        {
            ProcessEntry(entry, ref totalWork, ref workStart, ref lunchStart);
        }

        // If currently working (no clock out yet), calculate up to now
        if (currentWorkEndTime.HasValue && workStart != null)
        {
            totalWork += currentWorkEndTime.Value - workStart.Value;
        }

        return totalWork;
    }

    private static void ProcessEntry(TimeEntry entry, ref TimeSpan totalWork, ref DateTime? workStart, ref DateTime? lunchStart)
    {
        switch (entry.EventType)
        {
            case TimeEntryType.ClockIn:
                if (workStart == null) workStart = entry.Timestamp;
                break;

            case TimeEntryType.StartLunch:
                if (workStart != null)
                {
                    totalWork += entry.Timestamp - workStart.Value;
                    workStart = null;
                }
                lunchStart = entry.Timestamp;
                break;

            case TimeEntryType.EndLunch:
                if (lunchStart != null)
                {
                    lunchStart = null;
                    workStart = entry.Timestamp;
                }
                break;

            case TimeEntryType.ClockOut:
                if (workStart != null)
                {
                    totalWork += entry.Timestamp - workStart.Value;
                    workStart = null;
                }
                lunchStart = null;
                break;
        }
    }

    public async Task<IEnumerable<TimeEntry>> GetUserHistoryAsync(int userId, DateTime start, DateTime end)
    {
        return await _repository.GetHistoryAsync(userId, start, end);
    }
}
