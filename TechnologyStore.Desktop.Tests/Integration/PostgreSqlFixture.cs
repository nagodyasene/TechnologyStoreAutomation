using Testcontainers.PostgreSql;
using Npgsql;

namespace TechnologyStore.Desktop.Tests.Integration;

/// <summary>
/// Shared PostgreSQL container fixture for integration tests.
/// Uses Testcontainers to spin up a real PostgreSQL database in Docker.
/// Implements IAsyncLifetime for proper setup/teardown.
/// </summary>
public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    
    private const string DatabaseSchema = @"
        -- Create ENUM type for lifecycle phases (if not exists)
        DO $$ BEGIN
            CREATE TYPE lifecycle_phase_type AS ENUM ('ACTIVE', 'LEGACY', 'OBSOLETE');
        EXCEPTION
            WHEN duplicate_object THEN null;
        END $$;

        -- Products table
        CREATE TABLE IF NOT EXISTS products (
            id SERIAL PRIMARY KEY,
            name VARCHAR(200) NOT NULL,
            sku VARCHAR(100) UNIQUE NOT NULL,
            category VARCHAR(100),
            unit_price DECIMAL(10, 2) NOT NULL DEFAULT 0.00,
            current_stock INT NOT NULL DEFAULT 0,
            lifecycle_phase lifecycle_phase_type NOT NULL DEFAULT 'ACTIVE',
            successor_product_id INT REFERENCES products(id),
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            last_updated TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        -- Sales Transactions table
        CREATE TABLE IF NOT EXISTS sales_transactions (
            id SERIAL PRIMARY KEY,
            product_id INT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
            quantity_sold INT NOT NULL CHECK (quantity_sold > 0),
            total_amount DECIMAL(10, 2) NOT NULL,
            sale_date DATE NOT NULL DEFAULT CURRENT_DATE,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            notes TEXT
        );

        -- Inventory Transactions table
        CREATE TABLE IF NOT EXISTS inventory_transactions (
            id SERIAL PRIMARY KEY,
            product_id INT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
            quantity_change INT NOT NULL,
            transaction_type VARCHAR(50) NOT NULL,
            transaction_date DATE NOT NULL DEFAULT CURRENT_DATE,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            notes TEXT
        );

        -- Daily Summaries table
        CREATE TABLE IF NOT EXISTS daily_summaries (
            id SERIAL PRIMARY KEY,
            summary_date DATE NOT NULL,
            product_id INT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
            closing_stock INT NOT NULL DEFAULT 0,
            total_sold INT NOT NULL DEFAULT 0,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE(summary_date, product_id)
        );

        -- Lifecycle Audit Log
        CREATE TABLE IF NOT EXISTS lifecycle_audit_log (
            id SERIAL PRIMARY KEY,
            product_id INT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
            old_phase lifecycle_phase_type,
            new_phase lifecycle_phase_type NOT NULL,
            reason TEXT,
            changed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        -- Create indexes
        CREATE INDEX IF NOT EXISTS idx_sales_product_date ON sales_transactions(product_id, sale_date DESC);
        CREATE INDEX IF NOT EXISTS idx_daily_summaries_date ON daily_summaries(summary_date DESC, product_id);
        CREATE INDEX IF NOT EXISTS idx_inventory_transactions_product ON inventory_transactions(product_id, transaction_date DESC);
        CREATE INDEX IF NOT EXISTS idx_lifecycle_audit_product ON lifecycle_audit_log(product_id, changed_at DESC);
    ";
    
    public string ConnectionString => _container.GetConnectionString();

    public PostgreSqlFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("techstore_test")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await InitializeDatabaseSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates the database schema for testing
    /// </summary>
    private async Task InitializeDatabaseSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(DatabaseSchema, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Clears all data from the database (for test isolation)
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var resetSql = @"
            TRUNCATE TABLE lifecycle_audit_log CASCADE;
            TRUNCATE TABLE daily_summaries CASCADE;
            TRUNCATE TABLE inventory_transactions CASCADE;
            TRUNCATE TABLE sales_transactions CASCADE;
            TRUNCATE TABLE products CASCADE;
            ALTER SEQUENCE products_id_seq RESTART WITH 1;
            ALTER SEQUENCE sales_transactions_id_seq RESTART WITH 1;
            ALTER SEQUENCE inventory_transactions_id_seq RESTART WITH 1;
            ALTER SEQUENCE daily_summaries_id_seq RESTART WITH 1;
            ALTER SEQUENCE lifecycle_audit_log_id_seq RESTART WITH 1;
        ";

        await using var command = new NpgsqlCommand(resetSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Seeds the database with test data
    /// </summary>
    public async Task SeedTestDataAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var seedSql = @"
            INSERT INTO products (name, sku, category, unit_price, current_stock, lifecycle_phase) VALUES
            ('iPhone 15 Pro', 'TEST-IP15PRO', 'Smartphones', 999.99, 50, 'ACTIVE'),
            ('iPhone 14', 'TEST-IP14', 'Smartphones', 699.99, 25, 'LEGACY'),
            ('iPhone 12', 'TEST-IP12', 'Smartphones', 499.99, 10, 'OBSOLETE'),
            ('MacBook Pro', 'TEST-MBP', 'Laptops', 2499.99, 15, 'ACTIVE');
        ";

        await using var command = new NpgsqlCommand(seedSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Collection definition for sharing the PostgreSQL container across tests
    /// </summary>
    [CollectionDefinition("PostgreSQL")]
    public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
    {
    }
}

