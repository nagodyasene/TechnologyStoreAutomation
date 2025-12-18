using TechnologyStore.Desktop.Services;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TechnologyStore.Desktop.Features.Auth;

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

            if (!VerifyPassword(password, user.PasswordHash))
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
    /// Hashes a password using PBKDF2 with a random salt
    /// Format: salt:hash
    /// </summary>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return string.Empty;

        // Generate a random salt
        byte[] salt = RandomNumberGenerator.GetBytes(16);

        // Hash the password using PBKDF2
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);

        // Combine salt and hash
        return $"{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}";
    }

    /// <summary>
    /// Verifies a password against a stored hash (salt:hash)
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        try
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 2)
                return false;

            byte[] salt = Convert.FromHexString(parts[0]);
            byte[] storedPasswordHash = Convert.FromHexString(parts[1]);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            byte[] computedHash = pbkdf2.GetBytes(32);

            return CryptographicOperations.FixedTimeEquals(storedPasswordHash, computedHash);
        }
        catch
        {
            return false;
        }
    }
}
