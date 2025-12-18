using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TechnologyStoreAutomation.backend.leave;

/// <summary>
/// Repository for leave management data access using Dapper
/// </summary>
public class LeaveRepository : ILeaveRepository
{
    private readonly string _connectionString;
    private readonly ILogger<LeaveRepository> _logger;
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    public LeaveRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        _connectionString = connectionString;
        _logger = AppLogger.CreateLogger<LeaveRepository>();
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// Executes a database operation with retry logic for transient failures
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<IDbConnection, Task<T>> operation)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var connection = CreateConnection();
                return await operation(connection);
            }
            catch (NpgsqlException ex) when (IsTransientError(ex) && attempt < MaxRetries)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Transient database error on attempt {Attempt}, retrying...", attempt);
                await Task.Delay(RetryDelay * attempt);
            }
        }

        throw lastException ?? new InvalidOperationException("Database operation failed");
    }

    private static bool IsTransientError(NpgsqlException ex)
    {
        return ex.SqlState is "08000" or "08003" or "08006" or "40001" or "40P01";
    }

    public async Task<Employee?> GetEmployeeByUserIdAsync(int userId)
    {
        const string sql = @"
            SELECT e.id, e.user_id as UserId, e.employee_code as EmployeeCode, 
                   e.department, e.hire_date as HireDate, 
                   e.remaining_leave_days as RemainingLeaveDays, e.created_at as CreatedAt,
                   u.full_name as FullName
            FROM employees e
            JOIN users u ON e.user_id = u.id
            WHERE e.user_id = @UserId";

        return await ExecuteWithRetryAsync(async connection =>
            await connection.QueryFirstOrDefaultAsync<Employee>(sql, new { UserId = userId })
        );
    }

    public async Task<IEnumerable<Employee>> GetAllEmployeesAsync()
    {
        const string sql = @"
            SELECT e.id, e.user_id as UserId, e.employee_code as EmployeeCode, 
                   e.department, e.hire_date as HireDate, 
                   e.remaining_leave_days as RemainingLeaveDays, e.created_at as CreatedAt,
                   u.full_name as FullName
            FROM employees e
            JOIN users u ON e.user_id = u.id
            ORDER BY u.full_name";

        return await ExecuteWithRetryAsync(async connection =>
            await connection.QueryAsync<Employee>(sql)
        );
    }

    public async Task<int> CreateLeaveRequestAsync(LeaveRequest request)
    {
        // Validation: Check for overlapping requests
        const string checkOverlapSql = @"
            SELECT COUNT(1) 
            FROM leave_requests 
            WHERE employee_id = @EmployeeId 
            AND status != 'REJECTED'::leave_status
            AND (
                (start_date <= @EndDate AND end_date >= @StartDate)
            )";

        var hasOverlap = await ExecuteWithRetryAsync(async connection =>
            await connection.ExecuteScalarAsync<bool>(checkOverlapSql, new { request.EmployeeId, request.StartDate, request.EndDate })
        );

        if (hasOverlap)
        {
            throw new InvalidOperationException("A leave request already exists for this date range.");
        }

        const string sql = @"
            INSERT INTO leave_requests (employee_id, leave_type, start_date, end_date, total_days, reason, status)
            VALUES (@EmployeeId, @LeaveType::leave_type, @StartDate, @EndDate, @TotalDays, @Reason, 'PENDING'::leave_status)
            RETURNING id";

        var requestId = await ExecuteWithRetryAsync(async connection =>
            await connection.ExecuteScalarAsync<int>(sql, new
            {
                request.EmployeeId,
                LeaveType = request.LeaveType.ToString().ToUpper(),
                request.StartDate,
                request.EndDate,
                request.TotalDays,
                request.Reason
            })
        );

        _logger.LogInformation("Created leave request {RequestId} for employee {EmployeeId}", requestId, request.EmployeeId);
        return requestId;
    }

    public async Task<IEnumerable<LeaveRequest>> GetByEmployeeAsync(int employeeId)
    {
        const string sql = @"
            SELECT lr.id, lr.employee_id as EmployeeId, lr.leave_type::text as LeaveType,
                   lr.start_date as StartDate, lr.end_date as EndDate, lr.total_days as TotalDays,
                   lr.reason, lr.status::text as Status, lr.reviewed_by as ReviewedBy,
                   lr.review_comment as ReviewComment, lr.created_at as CreatedAt, lr.reviewed_at as ReviewedAt,
                   u.full_name as ReviewerName
            FROM leave_requests lr
            LEFT JOIN users u ON lr.reviewed_by = u.id
            WHERE lr.employee_id = @EmployeeId
            ORDER BY lr.created_at DESC";

        return await ExecuteWithRetryAsync(async connection =>
        {
            var requests = await connection.QueryAsync<LeaveRequestDto>(sql, new { EmployeeId = employeeId });
            return requests.Select(r => r.ToLeaveRequest());
        });
    }

    public async Task<IEnumerable<LeaveRequest>> GetPendingRequestsAsync()
    {
        return await GetAllRequestsAsync(LeaveStatus.Pending);
    }

    public async Task<IEnumerable<LeaveRequest>> GetAllRequestsAsync(LeaveStatus? statusFilter = null)
    {
        var sql = @"
            SELECT lr.id, lr.employee_id as EmployeeId, lr.leave_type::text as LeaveType,
                   lr.start_date as StartDate, lr.end_date as EndDate, lr.total_days as TotalDays,
                   lr.reason, lr.status::text as Status, lr.reviewed_by as ReviewedBy,
                   lr.review_comment as ReviewComment, lr.created_at as CreatedAt, lr.reviewed_at as ReviewedAt,
                   e_user.full_name as EmployeeName, r_user.full_name as ReviewerName
            FROM leave_requests lr
            JOIN employees e ON lr.employee_id = e.id
            JOIN users e_user ON e.user_id = e_user.id
            LEFT JOIN users r_user ON lr.reviewed_by = r_user.id";

        if (statusFilter.HasValue)
        {
            sql += " WHERE lr.status = @Status::leave_status";
        }

        sql += " ORDER BY lr.created_at DESC";

        return await ExecuteWithRetryAsync(async connection =>
        {
            var requests = await connection.QueryAsync<LeaveRequestDto>(
                sql, 
                statusFilter.HasValue ? new { Status = statusFilter.Value.ToString().ToUpper() } : null
            );
            return requests.Select(r => r.ToLeaveRequest());
        });
    }

    public async Task ApproveAsync(int requestId, int reviewerId, string? comment = null)
    {
        // 1. Get request details to check balance
        var request = await GetByIdAsync(requestId);
        if (request == null)
            throw new InvalidOperationException("Leave request not found");

        // 2. Check employee balance
        var employee = await GetEmployeeByUserIdAsync(request.EmployeeId); // Note: GetEmployeeByUserIdAsync takes userId not employeeId, wait.
        // Actually GetEmployeeByUserIdAsync queries by user_id. 'employee_id' in LeaveRequest relates to 'employees.id'.
        // I need a method to get employee by ID, or fix the usage.
        // Looking at GetByIdAsync, it joins employees e.
        // Let's create a helper or just query the balance directly here.
        
        const string checkBalanceSql = "SELECT remaining_leave_days FROM employees WHERE id = @Id";
        var remainingDays = await ExecuteWithRetryAsync(async connection => 
            await connection.ExecuteScalarAsync<int>(checkBalanceSql, new { Id = request.EmployeeId })
        );

        if (remainingDays < request.TotalDays)
        {
             throw new InvalidOperationException($"Insufficient leave balance. Remaining: {remainingDays}, Requested: {request.TotalDays}");
        }

        const string sql = @"
            UPDATE leave_requests 
            SET status = 'APPROVED'::leave_status, 
                reviewed_by = @ReviewerId, 
                review_comment = @Comment,
                reviewed_at = CURRENT_TIMESTAMP
            WHERE id = @RequestId AND status = 'PENDING'::leave_status";

        var affected = await ExecuteWithRetryAsync(async connection =>
            await connection.ExecuteAsync(sql, new { RequestId = requestId, ReviewerId = reviewerId, Comment = comment })
        );

        if (affected > 0)
        {
            _logger.LogInformation("Leave request {RequestId} approved by user {ReviewerId}", requestId, reviewerId);
            
            // Deduct leave days from employee
            await DeductLeaveDaysAsync(requestId);
        }
        else
        {
            throw new InvalidOperationException("Leave request not found or already processed");
        }
    }

    public async Task RejectAsync(int requestId, int reviewerId, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("Rejection comment is required", nameof(comment));

        const string sql = @"
            UPDATE leave_requests 
            SET status = 'REJECTED'::leave_status, 
                reviewed_by = @ReviewerId, 
                review_comment = @Comment,
                reviewed_at = CURRENT_TIMESTAMP
            WHERE id = @RequestId AND status = 'PENDING'::leave_status";

        var affected = await ExecuteWithRetryAsync(async connection =>
            await connection.ExecuteAsync(sql, new { RequestId = requestId, ReviewerId = reviewerId, Comment = comment })
        );

        if (affected > 0)
        {
            _logger.LogInformation("Leave request {RequestId} rejected by user {ReviewerId}", requestId, reviewerId);
        }
        else
        {
            throw new InvalidOperationException("Leave request not found or already processed");
        }
    }

    public async Task<LeaveRequest?> GetByIdAsync(int requestId)
    {
        const string sql = @"
            SELECT lr.id, lr.employee_id as EmployeeId, lr.leave_type::text as LeaveType,
                   lr.start_date as StartDate, lr.end_date as EndDate, lr.total_days as TotalDays,
                   lr.reason, lr.status::text as Status, lr.reviewed_by as ReviewedBy,
                   lr.review_comment as ReviewComment, lr.created_at as CreatedAt, lr.reviewed_at as ReviewedAt,
                   e_user.full_name as EmployeeName, r_user.full_name as ReviewerName
            FROM leave_requests lr
            JOIN employees e ON lr.employee_id = e.id
            JOIN users e_user ON e.user_id = e_user.id
            LEFT JOIN users r_user ON lr.reviewed_by = r_user.id
            WHERE lr.id = @RequestId";

        return await ExecuteWithRetryAsync(async connection =>
        {
            var dto = await connection.QueryFirstOrDefaultAsync<LeaveRequestDto>(sql, new { RequestId = requestId });
            return dto?.ToLeaveRequest();
        });
    }

    public async Task UpdateRemainingLeaveDaysAsync(int employeeId, int days)
    {
        const string sql = "UPDATE employees SET remaining_leave_days = @Days WHERE id = @EmployeeId";

        await ExecuteWithRetryAsync(async connection =>
        {
            await connection.ExecuteAsync(sql, new { EmployeeId = employeeId, Days = days });
            return true;
        });

        _logger.LogDebug("Updated remaining leave days for employee {EmployeeId} to {Days}", employeeId, days);
    }

    private async Task DeductLeaveDaysAsync(int requestId)
    {
        const string sql = @"
            UPDATE employees e
            SET remaining_leave_days = remaining_leave_days - lr.total_days
            FROM leave_requests lr
            WHERE lr.id = @RequestId AND lr.employee_id = e.id";

        await ExecuteWithRetryAsync(async connection =>
        {
            await connection.ExecuteAsync(sql, new { RequestId = requestId });
            return true;
        });
    }

    /// <summary>
    /// Internal DTO for mapping database results with enum handling
    /// </summary>
    private class LeaveRequestDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string LeaveType { get; set; } = "ANNUAL";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }
        public string? Reason { get; set; }
        public string Status { get; set; } = "PENDING";
        public int? ReviewedBy { get; set; }
        public string? ReviewComment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? EmployeeName { get; set; }
        public string? ReviewerName { get; set; }

        public LeaveRequest ToLeaveRequest() => new()
        {
            Id = Id,
            EmployeeId = EmployeeId,
            LeaveType = Enum.TryParse<LeaveType>(LeaveType, true, out var lt) ? lt : leave.LeaveType.Annual,
            StartDate = StartDate,
            EndDate = EndDate,
            TotalDays = TotalDays,
            Reason = Reason,
            Status = Enum.TryParse<LeaveStatus>(Status, true, out var ls) ? ls : leave.LeaveStatus.Pending,
            ReviewedBy = ReviewedBy,
            ReviewComment = ReviewComment,
            CreatedAt = CreatedAt,
            ReviewedAt = ReviewedAt,
            EmployeeName = EmployeeName,
            ReviewerName = ReviewerName
        };
    }
}
