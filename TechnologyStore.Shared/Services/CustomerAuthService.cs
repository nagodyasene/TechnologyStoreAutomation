using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Interfaces;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace TechnologyStore.Shared.Services;

/// <summary>
/// Authentication service for customer accounts
/// </summary>
public class CustomerAuthService : ICustomerAuthService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ILogger<CustomerAuthService> _logger;
    private Customer? _currentCustomer;
    private bool _isGuest;

    public CustomerAuthService(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _logger = AppLogger.CreateLogger<CustomerAuthService>();
    }

    public Customer? CurrentCustomer => _currentCustomer;
    public bool IsAuthenticated => _currentCustomer != null && !_isGuest;
    public bool IsGuest => _isGuest;

    public async Task<CustomerAuthResult> RegisterAsync(string email, string password, string fullName, string? phone)
    {
        if (string.IsNullOrWhiteSpace(email))
            return CustomerAuthResult.Failed("E-posta zorunludur.");
        
        if (string.IsNullOrWhiteSpace(password))
            return CustomerAuthResult.Failed("Şifre zorunludur.");
        
        if (password.Length < 6)
            return CustomerAuthResult.Failed("Şifre en az 6 karakter olmalıdır.");
        
        if (string.IsNullOrWhiteSpace(fullName))
            return CustomerAuthResult.Failed("Ad soyad zorunludur.");

        try
        {
            // Check if email already exists
            if (await _customerRepository.EmailExistsAsync(email))
            {
                return CustomerAuthResult.Failed("Bu e-posta ile kayıtlı bir hesap zaten var.");
            }

            var customer = new Customer
            {
                Email = email,
                PasswordHash = HashPassword(password),
                FullName = fullName.Trim(),
                Phone = phone?.Trim(),
                IsGuest = false
            };

            var created = await _customerRepository.CreateAsync(customer);
            
            _currentCustomer = created;
            _isGuest = false;
            
            _logger.LogInformation("New customer registered: {Email}", email);
            return CustomerAuthResult.Succeeded(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for {Email}", email);
            return CustomerAuthResult.Failed("Kayıt sırasında bir hata oluştu. Lütfen tekrar deneyin.");
        }
    }

    public async Task<CustomerAuthResult> LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email))
            return CustomerAuthResult.Failed("E-posta zorunludur.");
        
        if (string.IsNullOrWhiteSpace(password))
            return CustomerAuthResult.Failed("Şifre zorunludur.");

        try
        {
            var customer = await _customerRepository.GetByEmailAsync(email);
            
            if (customer == null)
            {
                _logger.LogWarning("Login failed: Customer not found - {Email}", email);
                return CustomerAuthResult.Failed("E-posta veya şifre hatalı.");
            }

            if (customer.IsGuest)
            {
                return CustomerAuthResult.Failed("Bu e-posta misafir ödeme için kullanılmış. Lütfen hesap oluşturun.");
            }

            if (!customer.IsActive)
            {
                _logger.LogWarning("Login failed: Account deactivated - {Email}", email);
                return CustomerAuthResult.Failed("Bu hesap pasif hale getirilmiş.");
            }

            if (string.IsNullOrEmpty(customer.PasswordHash) || !VerifyPassword(password, customer.PasswordHash))
            {
                _logger.LogWarning("Login failed: Invalid password - {Email}", email);
                return CustomerAuthResult.Failed("E-posta veya şifre hatalı.");
            }

            await _customerRepository.UpdateLastLoginAsync(customer.Id);
            customer.LastLogin = DateTime.UtcNow;

            _currentCustomer = customer;
            _isGuest = false;

            _logger.LogInformation("Customer logged in: {Email}", email);
            return CustomerAuthResult.Succeeded(customer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for {Email}", email);
            return CustomerAuthResult.Failed("Giriş sırasında bir hata oluştu. Lütfen tekrar deneyin.");
        }
    }

    public void Logout()
    {
        if (_currentCustomer != null)
        {
            _logger.LogInformation("Customer logged out: {Email}", _currentCustomer.Email);
        }
        _currentCustomer = null;
        _isGuest = false;
    }

    public async Task<Customer> GetOrCreateGuestAsync(string email, string fullName, string? phone)
    {
        var guest = await _customerRepository.CreateGuestAsync(email, fullName, phone);
        _currentCustomer = guest;
        _isGuest = true;
        
        _logger.LogInformation("Guest customer session started: {Email}", email);
        return guest;
    }

    /// <summary>
    /// Hashes a password using PBKDF2 with a random salt
    /// Format: salt:hash
    /// </summary>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return string.Empty;

        byte[] salt = RandomNumberGenerator.GetBytes(16);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);

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
