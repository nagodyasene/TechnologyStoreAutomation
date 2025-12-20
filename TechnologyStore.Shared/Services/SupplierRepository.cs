using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Services;

/// <summary>
/// PostgreSQL implementation of ISupplierRepository
/// </summary>
public class SupplierRepository : ISupplierRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SupplierRepository> _logger;

    public SupplierRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = AppLogger.CreateLogger<SupplierRepository>();
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    /// <inheritdoc />
    public async Task<IEnumerable<Supplier>> GetAllAsync(bool activeOnly = true)
    {
        using var db = CreateConnection();
        
        var sql = activeOnly
            ? @"SELECT id as Id, name as Name, email as Email, phone as Phone, 
                       contact_person as ContactPerson, address as Address,
                       lead_time_days as LeadTimeDays, is_active as IsActive,
                       created_at as CreatedAt, last_updated as LastUpdated
                FROM suppliers WHERE is_active = true ORDER BY name"
            : @"SELECT id as Id, name as Name, email as Email, phone as Phone, 
                       contact_person as ContactPerson, address as Address,
                       lead_time_days as LeadTimeDays, is_active as IsActive,
                       created_at as CreatedAt, last_updated as LastUpdated
                FROM suppliers ORDER BY name";
        
        return await db.QueryAsync<Supplier>(sql);
    }

    /// <inheritdoc />
    public async Task<Supplier?> GetByIdAsync(int id)
    {
        using var db = CreateConnection();
        
        const string sql = @"
            SELECT id as Id, name as Name, email as Email, phone as Phone, 
                   contact_person as ContactPerson, address as Address,
                   lead_time_days as LeadTimeDays, is_active as IsActive,
                   created_at as CreatedAt, last_updated as LastUpdated
            FROM suppliers WHERE id = @Id";
        
        return await db.QuerySingleOrDefaultAsync<Supplier>(sql, new { Id = id });
    }

    /// <inheritdoc />
    public async Task<Supplier> CreateAsync(Supplier supplier)
    {
        using var db = CreateConnection();
        
        const string sql = @"
            INSERT INTO suppliers (name, email, phone, contact_person, address, lead_time_days, is_active)
            VALUES (@Name, @Email, @Phone, @ContactPerson, @Address, @LeadTimeDays, @IsActive)
            RETURNING id";
        
        supplier.Id = await db.ExecuteScalarAsync<int>(sql, supplier);
        supplier.CreatedAt = DateTime.UtcNow;
        supplier.LastUpdated = DateTime.UtcNow;
        
        _logger.LogInformation("Created supplier: {Name} (ID: {Id})", supplier.Name, supplier.Id);
        return supplier;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Supplier supplier)
    {
        using var db = CreateConnection();
        
        const string sql = @"
            UPDATE suppliers 
            SET name = @Name, email = @Email, phone = @Phone, 
                contact_person = @ContactPerson, address = @Address,
                lead_time_days = @LeadTimeDays, is_active = @IsActive,
                last_updated = CURRENT_TIMESTAMP
            WHERE id = @Id";
        
        var rowsAffected = await db.ExecuteAsync(sql, supplier);
        
        if (rowsAffected > 0)
        {
            _logger.LogInformation("Updated supplier: {Name} (ID: {Id})", supplier.Name, supplier.Id);
        }
        
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id)
    {
        using var db = CreateConnection();
        
        // Soft delete - set is_active to false
        const string sql = @"
            UPDATE suppliers 
            SET is_active = false, last_updated = CURRENT_TIMESTAMP
            WHERE id = @Id";
        
        var rowsAffected = await db.ExecuteAsync(sql, new { Id = id });
        
        if (rowsAffected > 0)
        {
            _logger.LogInformation("Soft-deleted supplier ID: {Id}", id);
        }
        
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> EmailExistsAsync(string email, int? excludeId = null)
    {
        using var db = CreateConnection();
        
        var sql = excludeId.HasValue
            ? "SELECT COUNT(1) FROM suppliers WHERE LOWER(email) = LOWER(@Email) AND id != @ExcludeId"
            : "SELECT COUNT(1) FROM suppliers WHERE LOWER(email) = LOWER(@Email)";
        
        var count = await db.ExecuteScalarAsync<int>(sql, new { Email = email, ExcludeId = excludeId });
        return count > 0;
    }
}
