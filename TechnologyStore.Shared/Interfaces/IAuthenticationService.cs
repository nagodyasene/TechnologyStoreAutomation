using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Interface for authentication operations
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Attempts to authenticate a user with the given credentials
    /// </summary>
    Task<AuthResult> LoginAsync(string username, string password);

    /// <summary>
    /// Logs out the current user
    /// </summary>
    void Logout();

    /// <summary>
    /// Gets the currently authenticated user (null if not logged in)
    /// </summary>
    User? CurrentUser { get; }

    /// <summary>
    /// Returns true if a user is currently authenticated
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Returns true if the current user has admin privileges
    /// </summary>
    bool IsAdmin { get; }
}
