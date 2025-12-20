using System;

namespace TechnologyStore.Shared.Models
{
    public class PayrollRun
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
