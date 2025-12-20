using System;
using System.Threading.Tasks;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Desktop.Features.TimeTracking;

public interface ITimeTrackingService
{
    Task<TimeEntry> ClockInAsync(int userId);
    Task<TimeEntry> ClockOutAsync(int userId);
    Task<TimeEntry> StartLunchAsync(int userId);
    Task<TimeEntry> EndLunchAsync(int userId);
    Task<TimeEntry?> GetCurrentStatusAsync(int userId);
    Task<TimeSpan> CalculateDailyHoursAsync(int userId, DateTime date);
    Task<IEnumerable<TimeEntry>> GetUserHistoryAsync(int userId, DateTime start, DateTime end);
}
