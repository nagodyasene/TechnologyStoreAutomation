using System;

namespace TechnologyStore.Shared.Models
{
    public class PayrollEntry
    {
        public int Id { get; set; }
        public int PayrollRunId { get; set; }
        public int EmployeeId { get; set; }
        
        // Navigation / View Data
        public string EmployeeName { get; set; } = string.Empty;
        
        public decimal TotalHours { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal GrossPay { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
