using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using TechnologyStore.Desktop.Features.Leave; // for Employee
using TechnologyStore.Desktop.Features.TimeTracking; // for ITimeTrackingRepository or Service
using TechnologyStore.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace TechnologyStore.Desktop.Features.Payroll
{
    public class PayrollService : IPayrollService
    {
        private readonly string _connectionString;

        public PayrollService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                                ?? "Host=localhost;Database=techstore;Username=postgres;Password=password";
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<List<PayrollEntry>> PreviewPayrollAsync(DateTime start, DateTime end)
        {
            var entries = new List<PayrollEntry>();

            // 1. Get all employees (We use ILeaveRepository as it already has GetEmployees logic, or we could add GetEmployees to UserRepo)
            // Wait, ILeaveRepository doesn't expose GetAllEmployees. Let's use Dapper directly here for simplicity or add to repo.
            // Direct Dapper query is faster for this specific need.

            using var conn = CreateConnection();
            var sqlEmployees = @"
                SELECT e.*, u.full_name as FullName 
                FROM employees e 
                JOIN users u ON e.user_id = u.id 
                WHERE u.is_active = true";

            var employees = await conn.QueryAsync<EmployeeDto>(sqlEmployees);

            foreach (var emp in employees)
            {
                // 2. Calculate hours for range
                // We need a method in TimeTrackingService to calculate hours for a range. 
                // Currently it only has CalculateDailyHoursAsync. 
                // We should add 'CalculateTotalHoursAsync(userId, start, end)' to TimeTrackingService.
                // Or we can fetch all entries and sum them up.

                // For now, let's assume we implement the helper in this loop or extend the service.
                // Extending the service is cleaner but let's query entries here to avoid changing existing service too much right now.

                var workHours = await CalculateHoursForEmployee(conn, emp.UserId, start, end);

                var entry = new PayrollEntry
                {
                    EmployeeId = emp.Id,
                    EmployeeName = emp.FullName,
                    TotalHours = workHours,
                    HourlyRate = emp.HourlyRate,
                    GrossPay = workHours * emp.HourlyRate
                };
                entries.Add(entry);
            }

            return entries;
        }

        private async Task<decimal> CalculateHoursForEmployee(IDbConnection conn, int userId, DateTime start, DateTime end)
        {
            // Simple logic: Sum of completed shifts OR TimeEntries? 
            // TimeEntries is more accurate.
            // Fetch all entries in range
            var entries = await conn.QueryAsync<TimeEntry>(
                "SELECT * FROM time_entries WHERE user_id = @UserId AND timestamp BETWEEN @Start AND @End ORDER BY timestamp",
                new { UserId = userId, Start = start, End = end });

            // Using the same logic as TimeTrackingService.CalculateHoursFromEntries
            return (decimal)TimeTrackingService.CalculateHoursFromEntries(entries).TotalHours;
        }

        public async Task<int> CommitPayrollRunAsync(PayrollRun run, List<PayrollEntry> entries)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var trans = conn.BeginTransaction();

            try
            {
                // 1. Insert Run
                var sqlRun = @"
                    INSERT INTO payroll_runs (start_date, end_date, created_by, notes)
                    VALUES (@StartDate, @EndDate, @CreatedBy, @Notes)
                    RETURNING id;";

                var runId = await conn.ExecuteScalarAsync<int>(sqlRun, run, trans);

                // 2. Insert Entries
                var sqlEntry = @"
                    INSERT INTO payroll_entries (payroll_run_id, employee_id, total_hours, hourly_rate, gross_pay)
                    VALUES (@PayrollRunId, @EmployeeId, @TotalHours, @HourlyRate, @GrossPay)";

                foreach (var entry in entries)
                {
                    entry.PayrollRunId = runId;
                    await conn.ExecuteAsync(sqlEntry, entry, trans);
                }

                trans.Commit();
                return runId;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        public async Task<List<PayrollRun>> GetHistoryAsync()
        {
            using var conn = CreateConnection();
            return (await conn.QueryAsync<PayrollRun>("SELECT * FROM payroll_runs ORDER BY created_at DESC")).ToList();
        }

        // Helper class for the join
        private sealed class EmployeeDto : Employee
        {
            public string FullName { get; set; } = "";
        }
    }
}
