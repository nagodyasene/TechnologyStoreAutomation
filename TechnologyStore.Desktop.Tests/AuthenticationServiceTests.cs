using Moq;
using TechnologyStore.Desktop.Features.Auth;

namespace TechnologyStore.Desktop.Tests;

public class AuthenticationServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly AuthenticationService _authService;

    public AuthenticationServiceTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _authService = new AuthenticationService(_mockUserRepository.Object);
    }

    private static User CreateTestUser(
        int id = 1,
        string username = "testuser",
        string password = "password123",
        UserRole role = UserRole.Employee,
        bool isActive = true)
    {
        return new User
        {
            Id = id,
            Username = username,
            PasswordHash = AuthenticationService.HashPassword(password),
            FullName = "Test User",
            Role = role,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var user = CreateTestUser(password: "validpassword");
        _mockUserRepository
            .Setup(r => r.GetByUsernameAsync("testuser"))
            .ReturnsAsync(user);

        // Act
        var result = await _authService.LoginAsync("testuser", "validpassword");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("testuser", result.User.Username);
        Assert.True(_authService.IsAuthenticated);
        Assert.Equal(user, _authService.CurrentUser);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsFailure()
    {
        // Arrange
        var user = CreateTestUser(password: "correctpassword");
        _mockUserRepository
            .Setup(r => r.GetByUsernameAsync("testuser"))
            .ReturnsAsync(user);

        // Act
        var result = await _authService.LoginAsync("testuser", "wrongpassword");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.User);
        Assert.Equal("Invalid username or password.", result.ErrorMessage);
        Assert.False(_authService.IsAuthenticated);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        _mockUserRepository
            .Setup(r => r.GetByUsernameAsync("nonexistent"))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _authService.LoginAsync("nonexistent", "anypassword");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid username or password.", result.ErrorMessage);
        Assert.False(_authService.IsAuthenticated);
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsFailure()
    {
        // Arrange
        var inactiveUser = CreateTestUser(password: "password123", isActive: false);
        _mockUserRepository
            .Setup(r => r.GetByUsernameAsync("testuser"))
            .ReturnsAsync(inactiveUser);

        // Act
        var result = await _authService.LoginAsync("testuser", "password123");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("This account has been deactivated.", result.ErrorMessage);
        Assert.False(_authService.IsAuthenticated);
    }

    [Fact]
    public async Task Login_WithEmptyUsername_ReturnsFailure()
    {
        // Act
        var result = await _authService.LoginAsync("", "password");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Username is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task Login_WithEmptyPassword_ReturnsFailure()
    {
        // Act
        var result = await _authService.LoginAsync("username", "");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Password is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task Login_UpdatesLastLoginTime()
    {
        // Arrange
        var user = CreateTestUser(password: "password123");
        _mockUserRepository
            .Setup(r => r.GetByUsernameAsync("testuser"))
            .ReturnsAsync(user);

        // Act
        await _authService.LoginAsync("testuser", "password123");

        // Assert
        _mockUserRepository.Verify(r => r.UpdateLastLoginAsync(user.Id), Times.Once);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_ClearsCurrentUser()
    {
        // Arrange - Login first
        var user = CreateTestUser(password: "password123");
        _mockUserRepository
            .Setup(r => r.GetByUsernameAsync("testuser"))
            .ReturnsAsync(user);
        await _authService.LoginAsync("testuser", "password123");
        
        Assert.True(_authService.IsAuthenticated);

        // Act
        _authService.Logout();

        // Assert
        Assert.False(_authService.IsAuthenticated);
        Assert.Null(_authService.CurrentUser);
    }

    [Fact]
    public void Logout_WhenNotLoggedIn_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        var exception = Record.Exception(() => _authService.Logout());
        Assert.Null(exception);
    }

    #endregion

    #region IsAdmin Tests

    [Fact]
    public async Task IsAdmin_WithAdminUser_ReturnsTrue()
    {
        // Arrange
        var adminUser = CreateTestUser(password: "password123", role: UserRole.Admin);
        _mockUserRepository
            .Setup(r => r.GetByUsernameAsync("admin"))
            .ReturnsAsync(adminUser);

        // Act
        await _authService.LoginAsync("admin", "password123");

        // Assert
        Assert.True(_authService.IsAdmin);
    }

    [Fact]
    public async Task IsAdmin_WithEmployeeUser_ReturnsFalse()
    {
        // Arrange
        var employeeUser = CreateTestUser(password: "password123", role: UserRole.Employee);
        _mockUserRepository
            .Setup(r => r.GetByUsernameAsync("employee"))
            .ReturnsAsync(employeeUser);

        // Act
        await _authService.LoginAsync("employee", "password123");

        // Assert
        Assert.False(_authService.IsAdmin);
    }

    [Fact]
    public void IsAdmin_WhenNotLoggedIn_ReturnsFalse()
    {
        // Assert
        Assert.False(_authService.IsAdmin);
    }

    #endregion

    #region Password Hashing Tests

    [Fact]
    public void HashPassword_IsDeterministic()
    {
        // Arrange
        const string password = "testpassword123";

        // Act
        var hash1 = AuthenticationService.HashPassword(password);
        var hash2 = AuthenticationService.HashPassword(password);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashPassword_DifferentPasswordsProduceDifferentHashes()
    {
        // Arrange
        const string password1 = "password1";
        const string password2 = "password2";

        // Act
        var hash1 = AuthenticationService.HashPassword(password1);
        var hash2 = AuthenticationService.HashPassword(password2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashPassword_EmptyString_ReturnsEmptyString()
    {
        // Act
        var hash = AuthenticationService.HashPassword("");

        // Assert
        Assert.Equal(string.Empty, hash);
    }

    [Fact]
    public void HashPassword_ReturnsLowercaseHexString()
    {
        // Act
        var hash = AuthenticationService.HashPassword("test");

        // Assert
        Assert.Matches("^[a-f0-9]+$", hash);
        Assert.Equal(64, hash.Length); // SHA256 produces 64 hex characters
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AuthenticationService(null!));
    }

    #endregion
}
