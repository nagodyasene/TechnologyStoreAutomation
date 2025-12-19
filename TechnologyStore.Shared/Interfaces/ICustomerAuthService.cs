using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Interface for customer authentication
/// </summary>
public interface ICustomerAuthService
{
    /// <summary>
    /// Registers a new customer account
    /// </summary>
    Task<CustomerAuthResult> RegisterAsync(string email, string password, string fullName, string? phone);
    
    /// <summary>
    /// Attempts to log in a customer
    /// </summary>
    Task<CustomerAuthResult> LoginAsync(string email, string password);
    
    /// <summary>
    /// Logs out the current customer
    /// </summary>
    void Logout();
    
    /// <summary>
    /// Creates or retrieves a guest customer for checkout
    /// </summary>
    Task<Customer> GetOrCreateGuestAsync(string email, string fullName, string? phone);
    
    /// <summary>
    /// Gets the currently logged-in customer
    /// </summary>
    Customer? CurrentCustomer { get; }
    
    /// <summary>
    /// Returns true if a customer is logged in (not guest)
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Returns true if currently in guest mode
    /// </summary>
    bool IsGuest { get; }
}

/// <summary>
/// Result of a customer authentication operation
/// </summary>
public class CustomerAuthResult
{
    public bool Success { get; set; }
    public Customer? Customer { get; set; }
    public string? ErrorMessage { get; set; }
    
    public static CustomerAuthResult Succeeded(Customer customer) => new() { Success = true, Customer = customer };
    public static CustomerAuthResult Failed(string message) => new() { Success = false, ErrorMessage = message };
}
