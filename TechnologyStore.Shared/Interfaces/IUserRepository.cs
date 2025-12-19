using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Interface for user repository operations
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by username
    /// </summary>
    Task<User?> GetByUsernameAsync(string username);
    
    /// <summary>
    /// Gets a user by ID
    /// </summary>
    Task<User?> GetByIdAsync(int id);
    
    /// <summary>
    /// Updates the last login timestamp for a user
    /// </summary>
    Task UpdateLastLoginAsync(int userId);
    
    /// <summary>
    /// Creates a new user
    /// </summary>
    Task<User> CreateAsync(User user);
}
