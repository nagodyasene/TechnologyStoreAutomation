-- TechnologyStoreAutomation Database Schema
-- Run this SQL script in your PostgreSQL database to create the necessary tables

-- 1. Create ENUM type for lifecycle phases
CREATE TYPE lifecycle_phase_type AS ENUM ('ACTIVE', 'LEGACY', 'OBSOLETE');

-- 2. Products table
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

-- 3. Sales Transactions table (records each sale)
CREATE TABLE IF NOT EXISTS sales_transactions (
    id SERIAL PRIMARY KEY,
    product_id INT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    quantity_sold INT NOT NULL CHECK (quantity_sold > 0),
    total_amount DECIMAL(10, 2) NOT NULL,
    sale_date DATE NOT NULL DEFAULT CURRENT_DATE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    notes TEXT
);

-- 4. Inventory Transactions table (for full ledger tracking)
CREATE TABLE IF NOT EXISTS inventory_transactions (
    id SERIAL PRIMARY KEY,
    product_id INT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    quantity_change INT NOT NULL, -- Positive for restock, negative for sales
    transaction_type VARCHAR(50) NOT NULL, -- 'SALE', 'RESTOCK', 'ADJUSTMENT', 'RETURN'
    transaction_date DATE NOT NULL DEFAULT CURRENT_DATE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    notes TEXT
);

-- 5. Daily Summaries table (pre-calculated for fast dashboard queries)
CREATE TABLE IF NOT EXISTS daily_summaries (
    id SERIAL PRIMARY KEY,
    summary_date DATE NOT NULL,
    product_id INT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    closing_stock INT NOT NULL DEFAULT 0,
    total_sold INT NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(summary_date, product_id)
);

-- 6. Lifecycle Audit Log (tracks phase changes)
CREATE TABLE IF NOT EXISTS lifecycle_audit_log (
    id SERIAL PRIMARY KEY,
    product_id INT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    old_phase lifecycle_phase_type,
    new_phase lifecycle_phase_type NOT NULL,
    reason TEXT,
    changed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 7. Create indexes for performance
CREATE INDEX idx_sales_product_date ON sales_transactions(product_id, sale_date DESC);
CREATE INDEX idx_daily_summaries_date ON daily_summaries(summary_date DESC, product_id);
CREATE INDEX idx_inventory_transactions_product ON inventory_transactions(product_id, transaction_date DESC);
CREATE INDEX idx_lifecycle_audit_product ON lifecycle_audit_log(product_id, changed_at DESC);

-- 8. Sample data (optional - for testing)
INSERT INTO products (name, sku, category, unit_price, current_stock, lifecycle_phase) VALUES
('iPhone 15 Pro', 'APPLE-IP15PRO-256', 'Smartphones', 999.99, 45, 'ACTIVE'),
('iPhone 14', 'APPLE-IP14-128', 'Smartphones', 699.99, 23, 'LEGACY'),
('iPhone 12', 'APPLE-IP12-64', 'Smartphones', 499.99, 8, 'OBSOLETE'),
('Samsung Galaxy S24', 'SAMSUNG-S24-256', 'Smartphones', 899.99, 32, 'ACTIVE'),
('Google Pixel 8', 'GOOGLE-PIX8-128', 'Smartphones', 699.99, 18, 'ACTIVE'),
('MacBook Pro M3', 'APPLE-MBP-M3-16', 'Laptops', 2499.99, 12, 'ACTIVE'),
('Dell XPS 15', 'DELL-XPS15-I7', 'Laptops', 1799.99, 15, 'ACTIVE'),
('Sony WH-1000XM5', 'SONY-WH1000XM5', 'Audio', 399.99, 28, 'ACTIVE')
ON CONFLICT (sku) DO NOTHING;

-- 9. Sample sales transactions (last 14 days)
INSERT INTO sales_transactions (product_id, quantity_sold, total_amount, sale_date) VALUES
-- iPhone 15 Pro (trending well)
(1, 3, 2999.97, CURRENT_DATE - INTERVAL '1 day'),
(1, 2, 1999.98, CURRENT_DATE - INTERVAL '2 days'),
(1, 4, 3999.96, CURRENT_DATE - INTERVAL '3 days'),
(1, 2, 1999.98, CURRENT_DATE - INTERVAL '5 days'),
(1, 3, 2999.97, CURRENT_DATE - INTERVAL '7 days'),

-- iPhone 14 (declining legacy)
(2, 1, 699.99, CURRENT_DATE - INTERVAL '2 days'),
(2, 1, 699.99, CURRENT_DATE - INTERVAL '6 days'),

-- iPhone 12 (obsolete, slow moving)
(3, 1, 499.99, CURRENT_DATE - INTERVAL '8 days'),

-- Samsung Galaxy S24 (steady)
(4, 2, 1799.98, CURRENT_DATE - INTERVAL '1 day'),
(4, 3, 2699.97, CURRENT_DATE - INTERVAL '4 days'),
(4, 2, 1799.98, CURRENT_DATE - INTERVAL '7 days'),

-- Google Pixel 8 (moderate)
(5, 2, 1399.98, CURRENT_DATE - INTERVAL '3 days'),
(5, 1, 699.99, CURRENT_DATE - INTERVAL '6 days'),

-- MacBook Pro M3 (high value, slow turnover)
(6, 1, 2499.99, CURRENT_DATE - INTERVAL '2 days'),

-- Dell XPS 15
(7, 2, 3599.98, CURRENT_DATE - INTERVAL '5 days'),

-- Sony Headphones (popular accessories)
(8, 5, 1999.95, CURRENT_DATE - INTERVAL '1 day'),
(8, 3, 1199.97, CURRENT_DATE - INTERVAL '4 days'),
(8, 4, 1599.96, CURRENT_DATE - INTERVAL '6 days')
ON CONFLICT DO NOTHING;

COMMENT ON TABLE products IS 'Core product catalog with lifecycle tracking';
COMMENT ON TABLE sales_transactions IS 'Individual sales records for trend analysis';
COMMENT ON TABLE daily_summaries IS 'Pre-aggregated daily stats for fast dashboard queries';
COMMENT ON TABLE lifecycle_audit_log IS 'Tracks all product phase transitions';

