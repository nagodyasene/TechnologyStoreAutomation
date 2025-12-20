using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Interface for supplier repository operations
/// </summary>
public interface ISupplierRepository
{
    /// <summary>
    /// Gets all suppliers
    /// </summary>
    /// <param name="activeOnly">If true, only returns active suppliers</param>
    Task<IEnumerable<Supplier>> GetAllAsync(bool activeOnly = true);
    
    /// <summary>
    /// Gets a supplier by ID
    /// </summary>
    Task<Supplier?> GetByIdAsync(int id);
    
    /// <summary>
    /// Creates a new supplier
    /// </summary>
    Task<Supplier> CreateAsync(Supplier supplier);
    
    /// <summary>
    /// Updates an existing supplier
    /// </summary>
    Task<bool> UpdateAsync(Supplier supplier);
    
    /// <summary>
    /// Soft deletes a supplier (sets IsActive = false)
    /// </summary>
    Task<bool> DeleteAsync(int id);
    
    /// <summary>
    /// Checks if a supplier email already exists
    /// </summary>
    Task<bool> EmailExistsAsync(string email, int? excludeId = null);
}
