using TechnologyStore.Desktop.Services;
using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TechnologyStore.Desktop.Features.Auth;

/// <summary>
/// Repository for user data access using Dapper
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly string _connectionString;
    private readonly ILogger<UserRepository> _logger;
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    public UserRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        _connectionString = connectionString;
        _logger = AppLogger.CreateLogger<UserRepository>();
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

        throw lastException!;
    }

    /// <summary>
    /// Determines if a PostgreSQL exception is transient (can be retried)
    /// </summary>
    private static bool IsTransientError(NpgsqlException ex)
    {
        // PostgreSQL error codes for transient failures
        return ex.SqlState is "08000" // Connection exception
            or "08003" // Connection does not exist
            or "08006" // Connection failure
            or "40001" // Serialization failure
            or "40P01"; // Deadlock detected
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        const string sql = @"
            SELECT id, username, password_hash as PasswordHash, full_name as FullName, 
                   role::text as Role, is_active as IsActive, created_at as CreatedAt, last_login as LastLogin
            FROM users 
            WHERE LOWER(username) = LOWER(@Username) AND is_active = TRUE";

        return await ExecuteWithRetryAsync(async connection =>
        {
            var user = await connection.QueryFirstOrDefaultAsync<UserDto>(sql, new { Username = username });
            return user?.ToUser();
        });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        const string sql = @"
            SELECT id, username, password_hash as PasswordHash, full_name as FullName, 
                   role::text as Role, is_active as IsActive, created_at as CreatedAt, last_login as LastLogin
            FROM users 
            ORDER BY full_name";

        return await ExecuteWithRetryAsync(async connection =>
        {
            var users = await connection.QueryAsync<UserDto>(sql);
            return users.Select(u => u.ToUser());
        });
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        const string sql = "UPDATE users SET last_login = CURRENT_TIMESTAMP WHERE id = @UserId";

        await ExecuteWithRetryAsync(async connection =>
        {
            await connection.ExecuteAsync(sql, new { UserId = userId });
            return true;
        });

        _logger.LogDebug("Updated last login for user {UserId}", userId);
    }

    public async Task<int> CreateUserAsync(User user)
    {
        const string sql = @"
            INSERT INTO users (username, password_hash, full_name, role, is_active)
            VALUES (@Username, @PasswordHash, @FullName, @Role::user_role, @IsActive)
            RETURNING id";

        var userId = await ExecuteWithRetryAsync(async connection =>
        {
            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                user.Username,
                user.PasswordHash,
                user.FullName,
                Role = user.Role.ToString().ToUpper(),
                user.IsActive
            });
        });

        _logger.LogInformation("Created new user: {Username} (ID: {UserId})", user.Username, userId);
        return userId;
    }

    public async Task<bool> UpdatePasswordAsync(int userId, string newPasswordHash)
    {
        const string sql = "UPDATE users SET password_hash = @PasswordHash WHERE id = @UserId";

        var affected = await ExecuteWithRetryAsync(async connection =>
        {
            return await connection.ExecuteAsync(sql, new { UserId = userId, PasswordHash = newPasswordHash });
        });

        if (affected > 0)
        {
            _logger.LogInformation("Password updated for user {UserId}", userId);
        }

        return affected > 0;
    }

    /// <summary>
    /// Internal DTO for mapping database results with enum handling
    /// </summary>
    private class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "EMPLOYEE";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }

        public User ToUser() => new()
        {
            Id = Id,
            Username = Username,
            PasswordHash = PasswordHash,
            FullName = FullName,
            Role = Enum.TryParse<UserRole>(Role, true, out var role) ? role : UserRole.Employee,
            IsActive = IsActive,
            CreatedAt = CreatedAt,
            LastLogin = LastLogin
        };
    }
}
