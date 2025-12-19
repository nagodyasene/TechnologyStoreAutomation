using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TechnologyStore.Shared.Services;

/// <summary>
/// Repository for customer data access
/// </summary>
public class CustomerRepository : ICustomerRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CustomerRepository> _logger;

    public CustomerRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));
            
        _connectionString = connectionString;
        _logger = AppLogger.CreateLogger<CustomerRepository>();
    }

    private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    public async Task<Customer?> GetByEmailAsync(string email)
    {
        using var db = CreateConnection();
        var sql = @"
            SELECT id as Id, email as Email, password_hash as PasswordHash, 
                   full_name as FullName, phone as Phone, is_guest as IsGuest,
                   is_active as IsActive, created_at as CreatedAt, last_login as LastLogin
            FROM customers
            WHERE email = @Email;";
        
        return await db.QueryFirstOrDefaultAsync<Customer>(sql, new { Email = email.ToLowerInvariant() });
    }

    public async Task<Customer?> GetByIdAsync(int id)
    {
        using var db = CreateConnection();
        var sql = @"
            SELECT id as Id, email as Email, password_hash as PasswordHash, 
                   full_name as FullName, phone as Phone, is_guest as IsGuest,
                   is_active as IsActive, created_at as CreatedAt, last_login as LastLogin
            FROM customers
            WHERE id = @Id;";
        
        return await db.QueryFirstOrDefaultAsync<Customer>(sql, new { Id = id });
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        using var db = CreateConnection();
        var sql = @"
            INSERT INTO customers (email, password_hash, full_name, phone, is_guest, is_active)
            VALUES (@Email, @PasswordHash, @FullName, @Phone, @IsGuest, @IsActive)
            RETURNING id;";
        
        customer.Email = customer.Email.ToLowerInvariant();
        customer.Id = await db.ExecuteScalarAsync<int>(sql, customer);
        
        _logger.LogInformation("Created customer: {Email} (Guest: {IsGuest})", customer.Email, customer.IsGuest);
        return customer;
    }

    public async Task<Customer> CreateGuestAsync(string email, string fullName, string? phone)
    {
        // Check if guest already exists
        var existing = await GetByEmailAsync(email);
        if (existing != null && existing.IsGuest)
        {
            // Update existing guest info
            using var db = CreateConnection();
            var updateSql = @"
                UPDATE customers 
                SET full_name = @FullName, phone = @Phone
                WHERE id = @Id;";
            await db.ExecuteAsync(updateSql, new { Id = existing.Id, FullName = fullName, Phone = phone });
            existing.FullName = fullName;
            existing.Phone = phone;
            return existing;
        }
        
        if (existing != null)
        {
            // Email belongs to a registered user
            throw new InvalidOperationException("Email is already registered. Please log in.");
        }

        var customer = new Customer
        {
            Email = email,
            FullName = fullName,
            Phone = phone,
            IsGuest = true,
            PasswordHash = null
        };

        return await CreateAsync(customer);
    }

    public async Task UpdateLastLoginAsync(int customerId)
    {
        using var db = CreateConnection();
        var sql = "UPDATE customers SET last_login = CURRENT_TIMESTAMP WHERE id = @Id;";
        await db.ExecuteAsync(sql, new { Id = customerId });
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        using var db = CreateConnection();
        var sql = "SELECT COUNT(1) FROM customers WHERE email = @Email;";
        var count = await db.ExecuteScalarAsync<int>(sql, new { Email = email.ToLowerInvariant() });
        return count > 0;
    }
}
