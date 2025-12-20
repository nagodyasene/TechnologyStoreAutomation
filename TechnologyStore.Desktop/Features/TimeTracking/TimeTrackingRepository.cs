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
        var result = await connection.QuerySingleAsync<dynamic>(sql, entry);

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
            SELECT * FROM time_entries
            WHERE user_id = @UserId 
            AND timestamp >= @Start AND timestamp < @End
            ORDER BY timestamp";

        using var connection = CreateConnection();
        return await connection.QueryAsync<TimeEntry>(sql, new { UserId = userId, Start = start, End = end });
    }

    public async Task<IEnumerable<TimeEntry>> GetHistoryAsync(int userId, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT * FROM time_entries
            WHERE user_id = @UserId 
            AND timestamp BETWEEN @StartDate AND @EndDate
            ORDER BY timestamp DESC";

        using var connection = CreateConnection();
        return await connection.QueryAsync<TimeEntry>(sql, new { UserId = userId, StartDate = startDate, EndDate = endDate });
    }

    public async Task<TimeEntry?> GetLastEventAsync(int userId)
    {
        const string sql = @"
            SELECT * FROM time_entries
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
        await connection.ExecuteAsync(sql, entry);
    }
}
