-- TechnologyStoreAutomation - Authentication Schema Extension
-- Run this SQL script to add authentication tables to your PostgreSQL database
-- 1. Create ENUM type for user roles
DO $$ BEGIN IF NOT EXISTS (
    SELECT 1
    FROM pg_type
    WHERE typname = 'user_role'
) THEN CREATE TYPE user_role AS ENUM ('ADMIN', 'EMPLOYEE');
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
        'C8189E457E8AFB24741A7F67673187C6:9CAD8A60BD2C9AEE7A2E8537FA5D8267A306BC019C90CEE1B274912C4A9BD516',
        -- PBKDF2('admin123')
        'System Administrator',
        'ADMIN'
    ) ON CONFLICT (username) DO NOTHING;
-- 5. Seed a sample employee user (password: 'employee123')
INSERT INTO users (username, password_hash, full_name, role)
VALUES (
        'employee',
        'AFA409179958812CAFB0FC28FB066CB5:BEFFD76F9EE9428990C4AEC58477010C96E330BCED6DA8E688771533E2102C84',
        -- PBKDF2('employee123')
        'Sample Employee',
        'EMPLOYEE'
    ) ON CONFLICT (username) DO NOTHING;
COMMENT ON TABLE users IS 'User accounts for authentication and authorization';