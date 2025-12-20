using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Shared.Interfaces;

public interface IWorkShiftRepository
{
    Task<WorkShift> CreateAsync(WorkShift shift);
    Task<WorkShift?> GetByIdAsync(int id);
    Task<IEnumerable<WorkShift>> GetByUserAsync(int userId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<WorkShift>> GetAllAsync(DateTime startDate, DateTime endDate);
    Task UpdateAsync(WorkShift shift);
    Task DeleteAsync(int id);
}
