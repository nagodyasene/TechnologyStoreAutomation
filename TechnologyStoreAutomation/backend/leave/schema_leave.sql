-- TechnologyStoreAutomation - Leave Management Schema Extension
-- Run this SQL script to add leave management tables to your PostgreSQL database

-- 1. Create ENUM types for leave status and type
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'leave_status') THEN
        CREATE TYPE leave_status AS ENUM ('PENDING', 'APPROVED', 'REJECTED');
    END IF;
    
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'leave_type') THEN
        CREATE TYPE leave_type AS ENUM ('ANNUAL', 'SICK', 'PERSONAL', 'UNPAID');
    END IF;
END $$;

-- 2. Employees table (links users to employee records)
CREATE TABLE IF NOT EXISTS employees (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id) ON DELETE CASCADE,
    employee_code VARCHAR(50) UNIQUE NOT NULL,
    department VARCHAR(100),
    hire_date DATE NOT NULL,
    remaining_leave_days INT NOT NULL DEFAULT 14,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 3. Leave Requests table
CREATE TABLE IF NOT EXISTS leave_requests (
    id SERIAL PRIMARY KEY,
    employee_id INT NOT NULL REFERENCES employees(id) ON DELETE CASCADE,
    leave_type leave_type NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    total_days INT NOT NULL,
    reason TEXT,
    status leave_status NOT NULL DEFAULT 'PENDING',
    reviewed_by INT REFERENCES users(id),
    review_comment TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    reviewed_at TIMESTAMP,
    CONSTRAINT valid_dates CHECK (end_date >= start_date),
    CONSTRAINT valid_days CHECK (total_days > 0)
);

-- 4. Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_employees_user_id ON employees(user_id);
CREATE INDEX IF NOT EXISTS idx_leave_requests_employee ON leave_requests(employee_id);
CREATE INDEX IF NOT EXISTS idx_leave_requests_status ON leave_requests(status);
CREATE INDEX IF NOT EXISTS idx_leave_requests_dates ON leave_requests(start_date, end_date);

-- 5. Seed sample employee records (linking to existing users)
-- Note: This assumes users table already has admin (id=1) and employee (id=2) users
INSERT INTO employees (user_id, employee_code, department, hire_date, remaining_leave_days)
VALUES 
    (1, 'EMP-001', 'Management', '2020-01-01', 20),
    (2, 'EMP-002', 'Sales', '2022-06-15', 14)
ON CONFLICT (employee_code) DO NOTHING;

COMMENT ON TABLE employees IS 'Employee records linked to user accounts';
COMMENT ON TABLE leave_requests IS 'Leave/vacation request tracking with approval workflow';
