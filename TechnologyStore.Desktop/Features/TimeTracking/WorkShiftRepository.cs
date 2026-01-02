using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using TechnologyStore.Desktop.Services;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Desktop.Features.TimeTracking;

public class WorkShiftRepository : IWorkShiftRepository
{
    private readonly string _connectionString;
    private readonly ILogger<WorkShiftRepository> _logger;

    public WorkShiftRepository(string connectionString)
    {
        _connectionString = connectionString;
        _logger = AppLogger.CreateLogger<WorkShiftRepository>();
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// Converts C# ShiftStatus enum to PostgreSQL shift_status string
    /// </summary>
    private static string ToPostgresStatus(ShiftStatus status) => status switch
    {
        ShiftStatus.Scheduled => "SCHEDULED",
        ShiftStatus.Completed => "COMPLETED",
        ShiftStatus.Absent => "ABSENT",
        ShiftStatus.Late => "LATE",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown shift status")
    };

    /// <summary>
    /// Converts PostgreSQL shift_status string to C# ShiftStatus enum
    /// </summary>
    private static ShiftStatus FromPostgresStatus(string status) => status switch
    {
        "SCHEDULED" => ShiftStatus.Scheduled,
        "COMPLETED" => ShiftStatus.Completed,
        "ABSENT" => ShiftStatus.Absent,
        "LATE" => ShiftStatus.Late,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown shift status")
    };

    public async Task<WorkShift> CreateAsync(WorkShift shift)
    {
        const string sql = @"
            INSERT INTO work_shifts (user_id, start_time, end_time, status, notes, created_by)
            VALUES (@UserId, @StartTime, @EndTime, @Status::shift_status, @Notes, @CreatedBy)
            RETURNING id, created_at";

        using var connection = CreateConnection();
        var result = await connection.QuerySingleAsync<dynamic>(sql, new
        {
            shift.UserId,
            shift.StartTime,
            shift.EndTime,
            Status = ToPostgresStatus(shift.Status),
            shift.Notes,
            shift.CreatedBy
        });

        shift.Id = result.id;
        shift.CreatedAt = result.created_at;

        _logger.LogInformation("Created work shift {ShiftId} for user {UserId}", shift.Id, shift.UserId);
        return shift;
    }

    public async Task<WorkShift?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT ws.id as Id, ws.user_id as UserId, ws.start_time as StartTime, 
                   ws.end_time as EndTime, ws.status::text as StatusStr, ws.notes as Notes, 
                   ws.created_at as CreatedAt, ws.created_by as CreatedBy,
                   u.full_name as EmployeeName
            FROM work_shifts ws
            JOIN users u ON ws.user_id = u.id
            WHERE ws.id = @Id";

        using var connection = CreateConnection();
        var result = await connection.QuerySingleOrDefaultAsync<WorkShiftDto>(sql, new { Id = id });
        return result == null ? null : MapToWorkShift(result);
    }

    public async Task<IEnumerable<WorkShift>> GetByUserAsync(int userId, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT ws.id as Id, ws.user_id as UserId, ws.start_time as StartTime, 
                   ws.end_time as EndTime, ws.status::text as StatusStr, ws.notes as Notes, 
                   ws.created_at as CreatedAt, ws.created_by as CreatedBy,
                   u.full_name as EmployeeName
            FROM work_shifts ws
            JOIN users u ON ws.user_id = u.id
            WHERE ws.user_id = @UserId 
            AND ws.start_time BETWEEN @StartDate AND @EndDate
            ORDER BY ws.start_time";

        using var connection = CreateConnection();
        var results = await connection.QueryAsync<WorkShiftDto>(sql, new { UserId = userId, StartDate = startDate, EndDate = endDate });
        return results.Select(MapToWorkShift);
    }

    public async Task<IEnumerable<WorkShift>> GetAllAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT ws.id as Id, ws.user_id as UserId, ws.start_time as StartTime, 
                   ws.end_time as EndTime, ws.status::text as StatusStr, ws.notes as Notes, 
                   ws.created_at as CreatedAt, ws.created_by as CreatedBy,
                   u.full_name as EmployeeName
            FROM work_shifts ws
            JOIN users u ON ws.user_id = u.id
            WHERE ws.start_time BETWEEN @StartDate AND @EndDate
            ORDER BY ws.start_time";

        using var connection = CreateConnection();
        var results = await connection.QueryAsync<WorkShiftDto>(sql, new { StartDate = startDate, EndDate = endDate });
        return results.Select(MapToWorkShift);
    }

    public async Task UpdateAsync(WorkShift shift)
    {
        const string sql = @"
            UPDATE work_shifts
            SET start_time = @StartTime,
                end_time = @EndTime,
                status = @Status::shift_status,
                notes = @Notes
            WHERE id = @Id";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            shift.Id,
            shift.StartTime,
            shift.EndTime,
            Status = ToPostgresStatus(shift.Status),
            shift.Notes
        });
    }

    public async Task DeleteAsync(int id)
    {
        const string sql = "DELETE FROM work_shifts WHERE id = @Id";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    /// <summary>
    /// Maps the DTO from database to the domain model
    /// </summary>
    private static WorkShift MapToWorkShift(WorkShiftDto dto) => new()
    {
        Id = dto.Id,
        UserId = dto.UserId,
        StartTime = dto.StartTime,
        EndTime = dto.EndTime,
        Status = FromPostgresStatus(dto.StatusStr),
        Notes = dto.Notes,
        CreatedAt = dto.CreatedAt,
        CreatedBy = dto.CreatedBy,
        EmployeeName = dto.EmployeeName
    };

    /// <summary>
    /// Internal DTO for mapping database results with string status
    /// </summary>
    private class WorkShiftDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string StatusStr { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public string? EmployeeName { get; set; }
    }
}
