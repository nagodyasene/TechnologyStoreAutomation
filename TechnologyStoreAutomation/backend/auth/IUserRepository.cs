namespace TechnologyStoreAutomation.backend.auth;

/// <summary>
/// Interface for user data access operations
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by their username
    /// </summary>
    Task<User?> GetByUsernameAsync(string username);

    /// <summary>
    /// Gets all users in the system
    /// </summary>
    Task<IEnumerable<User>> GetAllAsync();

    /// <summary>
    /// Updates the last login timestamp for a user
    /// </summary>
    Task UpdateLastLoginAsync(int userId);

    /// <summary>
    /// Creates a new user account
    /// </summary>
    Task<int> CreateUserAsync(User user);

    /// <summary>
    /// Updates a user's password
    /// </summary>
    Task<bool> UpdatePasswordAsync(int userId, string newPasswordHash);
}
