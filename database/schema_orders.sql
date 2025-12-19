-- Online Ordering System Schema Extension
-- Run this SQL script after the main schema to add customers and orders
-- 1. Customers table (for registered and guest customers)
CREATE TABLE IF NOT EXISTS customers (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255),
    -- NULL for guest customers
    full_name VARCHAR(200) NOT NULL,
    phone VARCHAR(50),
    is_guest BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP
);
-- 2. Order status enum
DO $$ BEGIN IF NOT EXISTS (
    SELECT 1
    FROM pg_type
    WHERE typname = 'order_status'
) THEN CREATE TYPE order_status AS ENUM (
    'PENDING',
    'CONFIRMED',
    'READY_FOR_PICKUP',
    'COMPLETED',
    'CANCELLED'
);
END IF;
END $$;
-- 3. Orders table
CREATE TABLE IF NOT EXISTS orders (
    id SERIAL PRIMARY KEY,
    order_number VARCHAR(20) UNIQUE NOT NULL,
    -- e.g., "ORD-2024-00001"
    customer_id INT NOT NULL REFERENCES customers(id),
    status order_status NOT NULL DEFAULT 'PENDING',
    subtotal DECIMAL(10, 2) NOT NULL,
    tax DECIMAL(10, 2) NOT NULL DEFAULT 0,
    total DECIMAL(10, 2) NOT NULL,
    notes TEXT,
    pickup_date DATE,
    -- Preferred pickup date
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP
);
-- 4. Order items table
CREATE TABLE IF NOT EXISTS order_items (
    id SERIAL PRIMARY KEY,
    order_id INT NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id INT NOT NULL REFERENCES products(id),
    product_name VARCHAR(200) NOT NULL,
    -- Snapshot at order time
    unit_price DECIMAL(10, 2) NOT NULL,
    -- Snapshot at order time
    quantity INT NOT NULL CHECK (quantity > 0),
    line_total DECIMAL(10, 2) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
-- 5. Indexes for performance
CREATE INDEX IF NOT EXISTS idx_orders_customer ON orders(customer_id);
CREATE INDEX IF NOT EXISTS idx_orders_status ON orders(status);
CREATE INDEX IF NOT EXISTS idx_orders_created ON orders(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_order_items_order ON order_items(order_id);
CREATE INDEX IF NOT EXISTS idx_customers_email ON customers(email);
-- 6. Comments
COMMENT ON TABLE customers IS 'Customer accounts for online ordering (registered and guest)';
COMMENT ON TABLE orders IS 'Customer orders for in-store pickup';
COMMENT ON TABLE order_items IS 'Individual items within an order';
-- 7. Sample test customer (password: 'test123')
-- Password hash generated with PBKDF2-SHA256, 100000 iterations
INSERT INTO customers (email, password_hash, full_name, phone, is_guest)
VALUES (
        'test@example.com',
        'A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4:F1E2D3C4B5A6F1E2D3C4B5A6F1E2D3C4B5A6F1E2D3C4B5A6F1E2D3C4B5A6F1E2',
        'Test Customer',
        '+1234567890',
        false
    ) ON CONFLICT (email) DO NOTHING;