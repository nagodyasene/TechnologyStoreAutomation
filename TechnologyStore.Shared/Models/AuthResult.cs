namespace TechnologyStore.Shared.Models;

/// <summary>
/// Result of an authentication attempt
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public User? User { get; set; }

    public static AuthResult Succeeded(User user) => new()
    {
        Success = true,
        User = user
    };

    public static AuthResult Failed(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}
