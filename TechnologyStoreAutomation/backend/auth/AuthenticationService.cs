using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TechnologyStoreAutomation.backend.auth;

/// <summary>
/// Authentication service handling login/logout and session management
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AuthenticationService> _logger;
    private User? _currentUser;

    public AuthenticationService(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = AppLogger.CreateLogger<AuthenticationService>();
    }

    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;
    public bool IsAdmin => _currentUser?.Role == UserRole.Admin;

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return AuthResult.Failed("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return AuthResult.Failed("Password is required.");
        }

        try
        {
            _logger.LogInformation("Login attempt for user: {Username}", username);

            var user = await _userRepository.GetByUsernameAsync(username);

            if (user == null)
            {
                _logger.LogWarning("Login failed: User not found - {Username}", username);
                return AuthResult.Failed("Invalid username or password.");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Login failed: User account is deactivated - {Username}", username);
                return AuthResult.Failed("This account has been deactivated.");
            }

            var passwordHash = HashPassword(password);
            if (user.PasswordHash != passwordHash)
            {
                _logger.LogWarning("Login failed: Invalid password for user - {Username}", username);
                return AuthResult.Failed("Invalid username or password.");
            }

            // Update last login time
            await _userRepository.UpdateLastLoginAsync(user.Id);
            user.LastLogin = DateTime.UtcNow;

            _currentUser = user;
            _logger.LogInformation("Login successful: {Username} (Role: {Role})", username, user.Role);

            return AuthResult.Succeeded(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Username}", username);
            return AuthResult.Failed("An error occurred during login. Please try again.");
        }
    }

    public void Logout()
    {
        if (_currentUser != null)
        {
            _logger.LogInformation("User logged out: {Username}", _currentUser.Username);
            _currentUser = null;
        }
    }

    /// <summary>
    /// Hashes a password using SHA256
    /// </summary>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return string.Empty;

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
