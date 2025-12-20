-- TechnologyStoreAutomation - Purchasing Module Schema
-- Run this SQL script in your PostgreSQL database to add purchasing functionality
-- 1. Suppliers table
CREATE TABLE IF NOT EXISTS suppliers (
    id SERIAL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    email VARCHAR(255) NOT NULL,
    phone VARCHAR(50),
    contact_person VARCHAR(100),
    address VARCHAR(500),
    lead_time_days INT NOT NULL DEFAULT 7,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_updated TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
-- 2. Add supplier_id to products table (one supplier per product)
ALTER TABLE products
ADD COLUMN IF NOT EXISTS supplier_id INT REFERENCES suppliers(id);
-- 3. Create ENUM type for purchase order status
DO $$ BEGIN CREATE TYPE purchase_order_status AS ENUM (
    'PENDING',
    'APPROVED',
    'SENT',
    'RECEIVED',
    'CANCELLED'
);
EXCEPTION
WHEN duplicate_object THEN null;
END $$;
-- 4. Purchase Orders table
CREATE TABLE IF NOT EXISTS purchase_orders (
    id SERIAL PRIMARY KEY,
    order_number VARCHAR(50) UNIQUE NOT NULL,
    supplier_id INT NOT NULL REFERENCES suppliers(id),
    status purchase_order_status NOT NULL DEFAULT 'PENDING',
    total_amount DECIMAL(12, 2) NOT NULL DEFAULT 0.00,
    notes TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    approved_at TIMESTAMP,
    approved_by_user_id INT REFERENCES users(id),
    sent_at TIMESTAMP,
    received_at TIMESTAMP,
    expected_delivery_date DATE
);
-- 5. Purchase Order Items table
CREATE TABLE IF NOT EXISTS purchase_order_items (
    id SERIAL PRIMARY KEY,
    purchase_order_id INT NOT NULL REFERENCES purchase_orders(id) ON DELETE CASCADE,
    product_id INT NOT NULL REFERENCES products(id),
    product_name VARCHAR(200),
    product_sku VARCHAR(100),
    quantity INT NOT NULL CHECK (quantity > 0),
    unit_cost DECIMAL(10, 2) NOT NULL CHECK (unit_cost > 0),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
-- 6. Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_suppliers_active ON suppliers(is_active);
CREATE INDEX IF NOT EXISTS idx_products_supplier ON products(supplier_id);
CREATE INDEX IF NOT EXISTS idx_purchase_orders_supplier ON purchase_orders(supplier_id);
CREATE INDEX IF NOT EXISTS idx_purchase_orders_status ON purchase_orders(status);
CREATE INDEX IF NOT EXISTS idx_purchase_order_items_order ON purchase_order_items(purchase_order_id);
-- 7. PO number sequence for generating unique order numbers
CREATE SEQUENCE IF NOT EXISTS po_number_seq START WITH 1;
-- 8. Sample suppliers (optional - for testing)
INSERT INTO suppliers (
        name,
        email,
        phone,
        contact_person,
        lead_time_days
    )
VALUES (
        'Apple Inc. Distribution',
        'orders@apple-dist.example.com',
        '+1-800-555-0100',
        'John Smith',
        5
    ),
    (
        'Samsung Electronics Supply',
        'purchasing@samsung-supply.example.com',
        '+1-800-555-0200',
        'Sarah Lee',
        7
    ),
    (
        'Sony Distribution Center',
        'orders@sony-dist.example.com',
        '+1-800-555-0300',
        'Mike Johnson',
        10
    ),
    (
        'Dell Technologies Wholesale',
        'wholesale@dell.example.com',
        '+1-800-555-0400',
        'Emily Chen',
        6
    ) ON CONFLICT DO NOTHING;
COMMENT ON TABLE suppliers IS 'Supplier contact and lead time information';
COMMENT ON TABLE purchase_orders IS 'Purchase orders sent to suppliers for restocking';
COMMENT ON TABLE purchase_order_items IS 'Line items for each purchase order';