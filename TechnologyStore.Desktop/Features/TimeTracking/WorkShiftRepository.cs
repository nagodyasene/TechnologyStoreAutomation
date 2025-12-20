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

    public async Task<WorkShift> CreateAsync(WorkShift shift)
    {
        const string sql = @"
            INSERT INTO work_shifts (user_id, start_time, end_time, status, notes, created_by)
            VALUES (@UserId, @StartTime, @EndTime, @Status::shift_status, @Notes, @CreatedBy)
            RETURNING id, created_at";

        using var connection = CreateConnection();
        var result = await connection.QuerySingleAsync<dynamic>(sql, shift);

        shift.Id = result.id;
        shift.CreatedAt = result.created_at;

        _logger.LogInformation("Created work shift {ShiftId} for user {UserId}", shift.Id, shift.UserId);
        return shift;
    }

    public async Task<WorkShift?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT ws.*, u.full_name as EmployeeName
            FROM work_shifts ws
            JOIN users u ON ws.user_id = u.id
            WHERE ws.id = @Id";

        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WorkShift>(sql, new { Id = id });
    }

    public async Task<IEnumerable<WorkShift>> GetByUserAsync(int userId, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT ws.*, u.full_name as EmployeeName
            FROM work_shifts ws
            JOIN users u ON ws.user_id = u.id
            WHERE ws.user_id = @UserId 
            AND ws.start_time BETWEEN @StartDate AND @EndDate
            ORDER BY ws.start_time";

        using var connection = CreateConnection();
        return await connection.QueryAsync<WorkShift>(sql, new { UserId = userId, StartDate = startDate, EndDate = endDate });
    }

    public async Task<IEnumerable<WorkShift>> GetAllAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT ws.*, u.full_name as EmployeeName
            FROM work_shifts ws
            JOIN users u ON ws.user_id = u.id
            WHERE ws.start_time BETWEEN @StartDate AND @EndDate
            ORDER BY ws.start_time";

        using var connection = CreateConnection();
        return await connection.QueryAsync<WorkShift>(sql, new { StartDate = startDate, EndDate = endDate });
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
        await connection.ExecuteAsync(sql, shift);
    }

    public async Task DeleteAsync(int id)
    {
        const string sql = "DELETE FROM work_shifts WHERE id = @Id";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }
}
