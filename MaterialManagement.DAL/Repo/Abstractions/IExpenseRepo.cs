using MaterialManagement.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface IExpenseRepo
    {
        Task<IEnumerable<Expense>> GetAllAsync();
        Task<Expense?> GetByIdAsync(int id);
        Task CreateAsync(Expense expense); // لم تعد ترجع شيئًا
        Task UpdateAsync(Expense expense); // لم تعد ترجع شيئًا
        Task DeleteAsync(int id); // لم تعد ترجع bool

        Task<decimal> GetTotalExpensesAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<Expense>> GetExpensesByDateRangeAsync(DateTime startDate, DateTime endDate);

        IQueryable<Expense> GetAsQueryable();
    }
}