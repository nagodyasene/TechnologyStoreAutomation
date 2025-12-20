-- TechnologyStoreAutomation - Payroll System Schema
-- Run this SQL script to add payroll functionality
-- 1. Add hourly_rate to employees table
ALTER TABLE employees
ADD COLUMN IF NOT EXISTS hourly_rate DECIMAL(10, 2) NOT NULL DEFAULT 15.00;
-- 2. Payroll Runs table (Tracks each pay period generation)
CREATE TABLE IF NOT EXISTS payroll_runs (
    id SERIAL PRIMARY KEY,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by INT REFERENCES users(id),
    -- Admin who ran it
    notes TEXT,
    CONSTRAINT valid_date_range CHECK (end_date >= start_date)
);
-- 3. Payroll Entries table (Individual paychecks)
CREATE TABLE IF NOT EXISTS payroll_entries (
    id SERIAL PRIMARY KEY,
    payroll_run_id INT NOT NULL REFERENCES payroll_runs(id) ON DELETE CASCADE,
    employee_id INT NOT NULL REFERENCES employees(id),
    total_hours DECIMAL(10, 2) NOT NULL DEFAULT 0,
    hourly_rate DECIMAL(10, 2) NOT NULL,
    gross_pay DECIMAL(10, 2) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
-- 4. Indexes
CREATE INDEX IF NOT EXISTS idx_payroll_runs_date ON payroll_runs(start_date, end_date);
CREATE INDEX IF NOT EXISTS idx_payroll_entries_run ON payroll_entries(payroll_run_id);
CREATE INDEX IF NOT EXISTS idx_payroll_entries_employee ON payroll_entries(employee_id);
-- 5. Comments
COMMENT ON TABLE payroll_runs IS 'History of generated payroll periods';
COMMENT ON TABLE payroll_entries IS 'Calculated gross pay for each employee in a run';