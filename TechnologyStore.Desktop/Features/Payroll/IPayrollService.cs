using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Desktop.Features.Payroll
{
    public interface IPayrollService
    {
        /// <summary>
        /// Calculates payroll for a given date range without saving it.
        /// </summary>
        Task<List<PayrollEntry>> PreviewPayrollAsync(DateTime start, DateTime end);

        /// <summary>
        /// Saves the payroll run and entries to the database.
        /// </summary>
        Task<int> CommitPayrollRunAsync(PayrollRun run, List<PayrollEntry> entries);

        /// <summary>
        /// Gets history of payroll runs.
        /// </summary>
        Task<List<PayrollRun>> GetHistoryAsync();
    }
}
