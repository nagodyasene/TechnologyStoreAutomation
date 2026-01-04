using System.Data;
using Dapper;
using Npgsql;
using TechnologyStore.Desktop.Features.Auth;

namespace TechnologyStore.Desktop.Features.HR;

public sealed class EmployeeManagementRepository
{
    private readonly string _connectionString;
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    public EmployeeManagementRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));
        _connectionString = connectionString;
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    private static bool IsTransientError(NpgsqlException ex)
        => ex.SqlState is "08000" or "08003" or "08006" or "40001" or "40P01";

    private async Task<T> ExecuteWithRetryAsync<T>(Func<IDbConnection, Task<T>> operation)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var conn = CreateConnection();
                return await operation(conn);
            }
            catch (NpgsqlException ex) when (IsTransientError(ex) && attempt < MaxRetries)
            {
                last = ex;
                await Task.Delay(RetryDelay * attempt);
            }
        }
        throw last ?? new InvalidOperationException("Veritabanı işlemi başarısız oldu.");
    }

    public async Task<List<EmployeeManagementRow>> GetAllAsync()
    {
        const string sql = @"
            SELECT
                e.id as EmployeeId,
                e.user_id as UserId,
                u.username as Username,
                u.full_name as FullName,
                u.role::text as RoleText,
                u.is_active as IsActive,
                e.employee_code as EmployeeCode,
                e.department as Department,
                e.hire_date::timestamp as HireDate,
                e.remaining_leave_days as RemainingLeaveDays,
                e.hourly_rate as HourlyRate
            FROM employees e
            JOIN users u ON u.id = e.user_id
            ORDER BY u.full_name;";

        return await ExecuteWithRetryAsync(async c =>
        {
            var rows = await c.QueryAsync<EmployeeManagementRow>(sql);
            return rows.ToList();
        });
    }

    public async Task<int> CreateAsync(EmployeeManagementCreateRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        return await ExecuteWithRetryAsync(async c =>
        {
            if (c is NpgsqlConnection npg)
            {
                await npg.OpenAsync();
                await using var tx = await npg.BeginTransactionAsync();
                try
                {
                    const string insertUserSql = @"
                        INSERT INTO users (username, password_hash, full_name, role, is_active)
                        VALUES (@Username, @PasswordHash, @FullName, @Role::user_role, @IsActive)
                        RETURNING id;";

                    var userId = await npg.ExecuteScalarAsync<int>(insertUserSql, new
                    {
                        request.Username,
                        PasswordHash = AuthenticationService.HashPassword(request.Password),
                        request.FullName,
                        Role = request.RoleText.ToUpperInvariant(),
                        request.IsActive
                    }, tx);

                    const string insertEmployeeSql = @"
                        INSERT INTO employees (user_id, employee_code, department, hire_date, remaining_leave_days, hourly_rate)
                        VALUES (@UserId, @EmployeeCode, @Department, @HireDate, @RemainingLeaveDays, @HourlyRate)
                        RETURNING id;";

                    var employeeId = await npg.ExecuteScalarAsync<int>(insertEmployeeSql, new
                    {
                        UserId = userId,
                        request.EmployeeCode,
                        request.Department,
                        request.HireDate,
                        request.RemainingLeaveDays,
                        request.HourlyRate
                    }, tx);

                    await tx.CommitAsync();
                    return employeeId;
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }

            // Fallback (shouldn't happen in this app)
            c.Open();
            using var tx2 = c.BeginTransaction();
            try
            {
                const string insertUserSql = @"
                    INSERT INTO users (username, password_hash, full_name, role, is_active)
                    VALUES (@Username, @PasswordHash, @FullName, @Role::user_role, @IsActive)
                    RETURNING id;";

                var userId = await c.ExecuteScalarAsync<int>(insertUserSql, new
                {
                    request.Username,
                    PasswordHash = AuthenticationService.HashPassword(request.Password),
                    request.FullName,
                    Role = request.RoleText.ToUpperInvariant(),
                    request.IsActive
                }, tx2);

                const string insertEmployeeSql = @"
                    INSERT INTO employees (user_id, employee_code, department, hire_date, remaining_leave_days, hourly_rate)
                    VALUES (@UserId, @EmployeeCode, @Department, @HireDate, @RemainingLeaveDays, @HourlyRate)
                    RETURNING id;";

                var employeeId = await c.ExecuteScalarAsync<int>(insertEmployeeSql, new
                {
                    UserId = userId,
                    request.EmployeeCode,
                    request.Department,
                    request.HireDate,
                    request.RemainingLeaveDays,
                    request.HourlyRate
                }, tx2);

                tx2.Commit();
                return employeeId;
            }
            catch
            {
                tx2.Rollback();
                throw;
            }
        });
    }

    public async Task<bool> UpdateAsync(EmployeeManagementUpdateRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        return await ExecuteWithRetryAsync(async c =>
        {
            if (c is NpgsqlConnection npg)
            {
                await npg.OpenAsync();
                await using var tx = await npg.BeginTransactionAsync();
                try
                {
                    const string updateUserSql = @"
                        UPDATE users
                        SET username = @Username,
                            full_name = @FullName,
                            role = @Role::user_role,
                            is_active = @IsActive
                        WHERE id = @UserId;";

                    await npg.ExecuteAsync(updateUserSql, new
                    {
                        request.UserId,
                        request.Username,
                        request.FullName,
                        Role = request.RoleText.ToUpperInvariant(),
                        request.IsActive
                    }, tx);

                    if (!string.IsNullOrWhiteSpace(request.NewPassword))
                    {
                        const string updatePasswordSql = @"
                            UPDATE users
                            SET password_hash = @PasswordHash
                            WHERE id = @UserId;";
                        await npg.ExecuteAsync(updatePasswordSql, new
                        {
                            request.UserId,
                            PasswordHash = AuthenticationService.HashPassword(request.NewPassword)
                        }, tx);
                    }

                    const string updateEmployeeSql = @"
                        UPDATE employees
                        SET employee_code = @EmployeeCode,
                            department = @Department,
                            hire_date = @HireDate,
                            remaining_leave_days = @RemainingLeaveDays,
                            hourly_rate = @HourlyRate
                        WHERE id = @EmployeeId;";

                    var affected = await npg.ExecuteAsync(updateEmployeeSql, new
                    {
                        request.EmployeeId,
                        request.EmployeeCode,
                        request.Department,
                        request.HireDate,
                        request.RemainingLeaveDays,
                        request.HourlyRate
                    }, tx);

                    await tx.CommitAsync();
                    return affected > 0;
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }

            c.Open();
            using var tx2 = c.BeginTransaction();
            try
            {
                const string updateUserSql = @"
                    UPDATE users
                    SET username = @Username,
                        full_name = @FullName,
                        role = @Role::user_role,
                        is_active = @IsActive
                    WHERE id = @UserId;";

                await c.ExecuteAsync(updateUserSql, new
                {
                    request.UserId,
                    request.Username,
                    request.FullName,
                    Role = request.RoleText.ToUpperInvariant(),
                    request.IsActive
                }, tx2);

                if (!string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    const string updatePasswordSql = @"
                        UPDATE users
                        SET password_hash = @PasswordHash
                        WHERE id = @UserId;";
                    await c.ExecuteAsync(updatePasswordSql, new
                    {
                        request.UserId,
                        PasswordHash = AuthenticationService.HashPassword(request.NewPassword)
                    }, tx2);
                }

                const string updateEmployeeSql = @"
                    UPDATE employees
                    SET employee_code = @EmployeeCode,
                        department = @Department,
                        hire_date = @HireDate,
                        remaining_leave_days = @RemainingLeaveDays,
                        hourly_rate = @HourlyRate
                    WHERE id = @EmployeeId;";

                var affected = await c.ExecuteAsync(updateEmployeeSql, new
                {
                    request.EmployeeId,
                    request.EmployeeCode,
                    request.Department,
                    request.HireDate,
                    request.RemainingLeaveDays,
                    request.HourlyRate
                }, tx2);

                tx2.Commit();
                return affected > 0;
            }
            catch
            {
                tx2.Rollback();
                throw;
            }
        });
    }

    public async Task<EmployeeDeleteResult> DeleteAsync(int employeeId)
    {
        return await ExecuteWithRetryAsync(async c =>
        {
            const string lookupSql = "SELECT user_id FROM employees WHERE id = @EmployeeId;";
            var userId = await c.ExecuteScalarAsync<int?>(lookupSql, new { EmployeeId = employeeId });
            if (!userId.HasValue) return EmployeeDeleteResult.NotFound;

            try
            {
                const string deleteUserSql = "DELETE FROM users WHERE id = @UserId;";
                var affected = await c.ExecuteAsync(deleteUserSql, new { UserId = userId.Value });
                return affected > 0 ? EmployeeDeleteResult.Deleted : EmployeeDeleteResult.NotFound;
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                // FK constraint: fallback to deactivation
                const string deactivateSql = "UPDATE users SET is_active = FALSE WHERE id = @UserId;";
                var affected = await c.ExecuteAsync(deactivateSql, new { UserId = userId.Value });
                return affected > 0 ? EmployeeDeleteResult.Deactivated : EmployeeDeleteResult.Failed;
            }
        });
    }
}

public sealed class EmployeeManagementCreateRequest
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RoleText { get; set; } = "EMPLOYEE";
    public bool IsActive { get; set; } = true;

    public string EmployeeCode { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime HireDate { get; set; } = DateTime.Today;
    public int RemainingLeaveDays { get; set; } = 14;
    public decimal HourlyRate { get; set; } = 15.00m;
}

public sealed class EmployeeManagementUpdateRequest
{
    public int EmployeeId { get; set; }
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleText { get; set; } = "EMPLOYEE";
    public bool IsActive { get; set; } = true;
    public string? NewPassword { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime HireDate { get; set; }
    public int RemainingLeaveDays { get; set; }
    public decimal HourlyRate { get; set; }
}

public enum EmployeeDeleteResult
{
    Deleted,
    Deactivated,
    NotFound,
    Failed
}


