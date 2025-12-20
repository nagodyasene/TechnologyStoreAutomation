using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using TechnologyStore.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace TechnologyStore.Kiosk
{
    // Minimal interface for Kiosk needs
    public interface IProductRepository
    {
        Task<Product?> GetBySkuAsync(string sku);
        Task RecordSaleAsync(int productId, int quantity, decimal total);
    }

    public class KioskProductRepository : IProductRepository
    {
        private readonly string _connectionString;

        public KioskProductRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") 
                                ?? "Host=localhost;Database=techstore;Username=postgres;Password=password";
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<Product?> GetBySkuAsync(string sku)
        {
            using var conn = CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Product>(
                "SELECT * FROM products WHERE sku = @Sku AND lifecycle_phase != 'OBSOLETE'", new { Sku = sku });
        }

        public async Task RecordSaleAsync(int productId, int quantity, decimal total)
        {
            using var conn = CreateConnection();
            var sql = @"
                INSERT INTO sales_transactions (product_id, quantity_sold, total_amount, sale_date)
                VALUES (@ProductId, @Quantity, @Total, CURRENT_DATE);
                
                UPDATE products 
                SET current_stock = current_stock - @Quantity,
                    last_updated = CURRENT_TIMESTAMP
                WHERE id = @ProductId;
            ";
            await conn.ExecuteAsync(sql, new { ProductId = productId, Quantity = quantity, Total = total });
        }
    }
}
