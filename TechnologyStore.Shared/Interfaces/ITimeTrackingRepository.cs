using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

public interface ITimeTrackingRepository
{
    Task<TimeEntry> LogEventAsync(TimeEntry entry);
    Task<IEnumerable<TimeEntry>> GetDailyEventsAsync(int userId, DateTime date);
    Task<IEnumerable<TimeEntry>> GetHistoryAsync(int userId, DateTime startDate, DateTime endDate);
    Task<TimeEntry?> GetLastEventAsync(int userId);
    Task UpdateEntryAsync(TimeEntry entry);
}
