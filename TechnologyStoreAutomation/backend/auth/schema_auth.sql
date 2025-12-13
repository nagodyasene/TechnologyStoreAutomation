-- TechnologyStoreAutomation - Authentication Schema Extension
-- Run this SQL script to add authentication tables to your PostgreSQL database

-- 1. Create ENUM type for user roles
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'user_role') THEN
        CREATE TYPE user_role AS ENUM ('ADMIN', 'EMPLOYEE');
    END IF;
END $$;

-- 2. Users table for authentication
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(256) NOT NULL,
    full_name VARCHAR(200) NOT NULL,
    role user_role NOT NULL DEFAULT 'EMPLOYEE',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP
);

-- 3. Create index for login queries
CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);

-- 4. Seed default admin user (password: 'admin123')
-- Password hash is SHA256 of 'admin123'
INSERT INTO users (username, password_hash, full_name, role) 
VALUES (
    'admin', 
    '240be518fabd2724ddb6f04eeb9d7d2b8b7e7df0a8c9b8f07bc3a85b14b986db', -- SHA256('admin123')
    'System Administrator', 
    'ADMIN'
)
ON CONFLICT (username) DO NOTHING;

-- 5. Seed a sample employee user (password: 'employee123')
INSERT INTO users (username, password_hash, full_name, role) 
VALUES (
    'employee', 
    'f3f4db2ccd59a3b9a5b2f0c8f4a9e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b3a2f1', -- Placeholder hash
    'Sample Employee', 
    'EMPLOYEE'
)
ON CONFLICT (username) DO NOTHING;

COMMENT ON TABLE users IS 'User accounts for authentication and authorization';
