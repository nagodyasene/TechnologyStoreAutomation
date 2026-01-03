using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using TechnologyStore.Desktop.Services;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Desktop.Features.TimeTracking;

public class TimeTrackingRepository : ITimeTrackingRepository
{
    private readonly string _connectionString;
    private readonly ILogger<TimeTrackingRepository> _logger;

    public TimeTrackingRepository(string connectionString)
    {
        _connectionString = connectionString;
        _logger = AppLogger.CreateLogger<TimeTrackingRepository>();
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    public async Task<TimeEntry> LogEventAsync(TimeEntry entry)
    {
        const string sql = @"
            INSERT INTO time_entries (user_id, event_type, timestamp, notes, is_manual_entry)
            VALUES (@UserId, @EventType::time_entry_type, @Timestamp, @Notes, @IsManualEntry)
            RETURNING id, created_at";

        using var connection = CreateConnection();
        // Dapper sends enums as integers by default; Postgres time_entry_type is an enum of TEXT values.
        // Always pass the enum as its upper-case string name so `@EventType::time_entry_type` casts correctly.
        var args = new
        {
            entry.UserId,
            EventType = ToDbEventType(entry.EventType),
            entry.Timestamp,
            entry.Notes,
            entry.IsManualEntry
        };
        var result = await connection.QuerySingleAsync<dynamic>(sql, args);

        entry.Id = result.id;
        entry.CreatedAt = result.created_at;

        _logger.LogInformation("Logged time entry {EntryId} ({EventType}) for user {UserId}", entry.Id, entry.EventType, entry.UserId);
        return entry;
    }

    public async Task<IEnumerable<TimeEntry>> GetDailyEventsAsync(int userId, DateTime date)
    {
        // Get events for the full 24 hours of that date
        var start = date.Date;
        var end = start.AddDays(1);

        const string sql = @"
            SELECT
                id as Id,
                user_id as UserId,
                CASE event_type::text
                    WHEN 'CLOCK_IN' THEN 'ClockIn'
                    WHEN 'CLOCK_OUT' THEN 'ClockOut'
                    WHEN 'START_LUNCH' THEN 'StartLunch'
                    WHEN 'END_LUNCH' THEN 'EndLunch'
                    ELSE 'ClockIn'
                END as EventType,
                timestamp as Timestamp,
                notes as Notes,
                is_manual_entry as IsManualEntry,
                created_at as CreatedAt
            FROM time_entries
            WHERE user_id = @UserId 
            AND timestamp >= @Start AND timestamp < @End
            ORDER BY timestamp";

        using var connection = CreateConnection();
        return await connection.QueryAsync<TimeEntry>(sql, new { UserId = userId, Start = start, End = end });
    }

    public async Task<IEnumerable<TimeEntry>> GetHistoryAsync(int userId, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT
                id as Id,
                user_id as UserId,
                CASE event_type::text
                    WHEN 'CLOCK_IN' THEN 'ClockIn'
                    WHEN 'CLOCK_OUT' THEN 'ClockOut'
                    WHEN 'START_LUNCH' THEN 'StartLunch'
                    WHEN 'END_LUNCH' THEN 'EndLunch'
                    ELSE 'ClockIn'
                END as EventType,
                timestamp as Timestamp,
                notes as Notes,
                is_manual_entry as IsManualEntry,
                created_at as CreatedAt
            FROM time_entries
            WHERE user_id = @UserId 
            AND timestamp BETWEEN @StartDate AND @EndDate
            ORDER BY timestamp DESC";

        using var connection = CreateConnection();
        return await connection.QueryAsync<TimeEntry>(sql, new { UserId = userId, StartDate = startDate, EndDate = endDate });
    }

    public async Task<TimeEntry?> GetLastEventAsync(int userId)
    {
        const string sql = @"
            SELECT
                id as Id,
                user_id as UserId,
                CASE event_type::text
                    WHEN 'CLOCK_IN' THEN 'ClockIn'
                    WHEN 'CLOCK_OUT' THEN 'ClockOut'
                    WHEN 'START_LUNCH' THEN 'StartLunch'
                    WHEN 'END_LUNCH' THEN 'EndLunch'
                    ELSE 'ClockIn'
                END as EventType,
                timestamp as Timestamp,
                notes as Notes,
                is_manual_entry as IsManualEntry,
                created_at as CreatedAt
            FROM time_entries
            WHERE user_id = @UserId
            ORDER BY timestamp DESC
            LIMIT 1";

        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TimeEntry>(sql, new { UserId = userId });
    }

    public async Task UpdateEntryAsync(TimeEntry entry)
    {
        const string sql = @"
            UPDATE time_entries
            SET timestamp = @Timestamp,
                event_type = @EventType::time_entry_type,
                notes = @Notes,
                is_manual_entry = @IsManualEntry
            WHERE id = @Id";

        using var connection = CreateConnection();
        var args = new
        {
            entry.Id,
            entry.Timestamp,
            EventType = ToDbEventType(entry.EventType),
            entry.Notes,
            entry.IsManualEntry
        };
        await connection.ExecuteAsync(sql, args);
    }

    private static string ToDbEventType(TimeEntryType type) =>
        type switch
        {
            TimeEntryType.ClockIn => "CLOCK_IN",
            TimeEntryType.ClockOut => "CLOCK_OUT",
            TimeEntryType.StartLunch => "START_LUNCH",
            TimeEntryType.EndLunch => "END_LUNCH",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported time entry type")
        };
}
