namespace TechnologyStore.Desktop.Features.Leave;

/// <summary>
/// Interface for leave management data access operations
/// </summary>
public interface ILeaveRepository
{
    /// <summary>
    /// Gets an employee by their user ID
    /// </summary>
    Task<Employee?> GetEmployeeByUserIdAsync(int userId);

    /// <summary>
    /// Gets all employees
    /// </summary>
    Task<IEnumerable<Employee>> GetAllEmployeesAsync();

    /// <summary>
    /// Creates a new leave request
    /// </summary>
    Task<int> CreateLeaveRequestAsync(LeaveRequest request);

    /// <summary>
    /// Gets leave requests for a specific employee
    /// </summary>
    Task<IEnumerable<LeaveRequest>> GetByEmployeeAsync(int employeeId);

    /// <summary>
    /// Gets all pending leave requests (for admin approval)
    /// </summary>
    Task<IEnumerable<LeaveRequest>> GetPendingRequestsAsync();

    /// <summary>
    /// Gets all leave requests with optional status filter
    /// </summary>
    Task<IEnumerable<LeaveRequest>> GetAllRequestsAsync(LeaveStatus? statusFilter = null);

    /// <summary>
    /// Approves a leave request
    /// </summary>
    Task ApproveAsync(int requestId, int reviewerId, string? comment = null);

    /// <summary>
    /// Rejects a leave request (comment is required)
    /// </summary>
    Task RejectAsync(int requestId, int reviewerId, string comment);

    /// <summary>
    /// Gets a single leave request by ID
    /// </summary>
    Task<LeaveRequest?> GetByIdAsync(int requestId);

    /// <summary>
    /// Updates an employee's remaining leave days
    /// </summary>
    Task UpdateRemainingLeaveDaysAsync(int employeeId, int days);
}
