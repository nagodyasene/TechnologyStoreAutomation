using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Interface for customer repository operations
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Gets a customer by email address
    /// </summary>
    Task<Customer?> GetByEmailAsync(string email);
    
    /// <summary>
    /// Gets a customer by ID
    /// </summary>
    Task<Customer?> GetByIdAsync(int id);
    
    /// <summary>
    /// Creates a new customer account
    /// </summary>
    Task<Customer> CreateAsync(Customer customer);
    
    /// <summary>
    /// Creates a guest customer record
    /// </summary>
    Task<Customer> CreateGuestAsync(string email, string fullName, string? phone);
    
    /// <summary>
    /// Updates the last login timestamp
    /// </summary>
    Task UpdateLastLoginAsync(int customerId);
    
    /// <summary>
    /// Checks if an email is already registered
    /// </summary>
    Task<bool> EmailExistsAsync(string email);
}
