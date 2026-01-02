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

    /// <summary>
    /// Converts C# TimeEntryType enum to PostgreSQL time_entry_type string
    /// </summary>
    private static string ToPostgresEventType(TimeEntryType eventType) => eventType switch
    {
        TimeEntryType.ClockIn => "CLOCK_IN",
        TimeEntryType.ClockOut => "CLOCK_OUT",
        TimeEntryType.StartLunch => "START_LUNCH",
        TimeEntryType.EndLunch => "END_LUNCH",
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unknown event type")
    };

    /// <summary>
    /// Converts PostgreSQL time_entry_type string to C# TimeEntryType enum
    /// </summary>
    private static TimeEntryType FromPostgresEventType(string eventType) => eventType switch
    {
        "CLOCK_IN" => TimeEntryType.ClockIn,
        "CLOCK_OUT" => TimeEntryType.ClockOut,
        "START_LUNCH" => TimeEntryType.StartLunch,
        "END_LUNCH" => TimeEntryType.EndLunch,
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unknown event type")
    };

    public async Task<TimeEntry> LogEventAsync(TimeEntry entry)
    {
        const string sql = @"
            INSERT INTO time_entries (user_id, event_type, timestamp, notes, is_manual_entry)
            VALUES (@UserId, @EventType::time_entry_type, @Timestamp, @Notes, @IsManualEntry)
            RETURNING id, created_at";

        using var connection = CreateConnection();
        var result = await connection.QuerySingleAsync<dynamic>(sql, new
        {
            entry.UserId,
            EventType = ToPostgresEventType(entry.EventType),
            entry.Timestamp,
            entry.Notes,
            entry.IsManualEntry
        });

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
            SELECT id as Id, user_id as UserId, event_type::text as EventTypeStr, 
                   timestamp as Timestamp, notes as Notes, is_manual_entry as IsManualEntry, 
                   created_at as CreatedAt
            FROM time_entries
            WHERE user_id = @UserId 
            AND timestamp >= @Start AND timestamp < @End
            ORDER BY timestamp";

        using var connection = CreateConnection();
        var results = await connection.QueryAsync<TimeEntryDto>(sql, new { UserId = userId, Start = start, End = end });
        return results.Select(MapToTimeEntry);
    }

    public async Task<IEnumerable<TimeEntry>> GetHistoryAsync(int userId, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT id as Id, user_id as UserId, event_type::text as EventTypeStr, 
                   timestamp as Timestamp, notes as Notes, is_manual_entry as IsManualEntry, 
                   created_at as CreatedAt
            FROM time_entries
            WHERE user_id = @UserId 
            AND timestamp BETWEEN @StartDate AND @EndDate
            ORDER BY timestamp DESC";

        using var connection = CreateConnection();
        var results = await connection.QueryAsync<TimeEntryDto>(sql, new { UserId = userId, StartDate = startDate, EndDate = endDate });
        return results.Select(MapToTimeEntry);
    }

    public async Task<TimeEntry?> GetLastEventAsync(int userId)
    {
        const string sql = @"
            SELECT id as Id, user_id as UserId, event_type::text as EventTypeStr, 
                   timestamp as Timestamp, notes as Notes, is_manual_entry as IsManualEntry, 
                   created_at as CreatedAt
            FROM time_entries
            WHERE user_id = @UserId
            ORDER BY timestamp DESC
            LIMIT 1";

        using var connection = CreateConnection();
        var result = await connection.QuerySingleOrDefaultAsync<TimeEntryDto>(sql, new { UserId = userId });
        return result == null ? null : MapToTimeEntry(result);
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
        await connection.ExecuteAsync(sql, new
        {
            entry.Id,
            entry.Timestamp,
            EventType = ToPostgresEventType(entry.EventType),
            entry.Notes,
            entry.IsManualEntry
        });
    }

    /// <summary>
    /// Maps the DTO from database to the domain model
    /// </summary>
    private static TimeEntry MapToTimeEntry(TimeEntryDto dto) => new()
    {
        Id = dto.Id,
        UserId = dto.UserId,
        EventType = FromPostgresEventType(dto.EventTypeStr),
        Timestamp = dto.Timestamp,
        Notes = dto.Notes,
        IsManualEntry = dto.IsManualEntry,
        CreatedAt = dto.CreatedAt
    };

    /// <summary>
    /// Internal DTO for mapping database results with string event_type
    /// </summary>
    private class TimeEntryDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string EventTypeStr { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Notes { get; set; }
        public bool IsManualEntry { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
