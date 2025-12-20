-- TechnologyStoreAutomation - Time Tracking Schema
-- Run this SQL script to add time tracking tables to your PostgreSQL database
-- 1. Create ENUM type for time entry types
DO $$ BEGIN IF NOT EXISTS (
    SELECT 1
    FROM pg_type
    WHERE typname = 'time_entry_type'
) THEN CREATE TYPE time_entry_type AS ENUM (
    'CLOCK_IN',
    'CLOCK_OUT',
    'START_LUNCH',
    'END_LUNCH'
);
END IF;
END $$;
-- 2. Create ENUM type for shift status
DO $$ BEGIN IF NOT EXISTS (
    SELECT 1
    FROM pg_type
    WHERE typname = 'shift_status'
) THEN CREATE TYPE shift_status AS ENUM ('SCHEDULED', 'COMPLETED', 'ABSENT', 'LATE');
END IF;
END $$;
-- 3. Work Shifts table (Manager assigned schedules)
CREATE TABLE IF NOT EXISTS work_shifts (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP NOT NULL,
    status shift_status NOT NULL DEFAULT 'SCHEDULED',
    notes TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by INT REFERENCES users(id),
    -- Manager who assigned it
    CONSTRAINT valid_shift_times CHECK (end_time > start_time)
);
-- 4. Time Entries table (Actual clock events)
CREATE TABLE IF NOT EXISTS time_entries (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    event_type time_entry_type NOT NULL,
    timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    notes TEXT,
    is_manual_entry BOOLEAN DEFAULT FALSE,
    -- Adjusted by manager?
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
-- 5. Indexes for performance
CREATE INDEX IF NOT EXISTS idx_work_shifts_user_date ON work_shifts(user_id, start_time);
CREATE INDEX IF NOT EXISTS idx_time_entries_user_date ON time_entries(user_id, timestamp);
COMMENT ON TABLE work_shifts IS 'Scheduled work shifts assigned by managers';
COMMENT ON TABLE time_entries IS 'Actual clock-in/out logs for precise time tracking';