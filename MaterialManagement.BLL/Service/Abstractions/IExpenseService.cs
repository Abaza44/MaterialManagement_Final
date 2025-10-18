using MaterialManagement.BLL.ModelVM.Expense;
using MaterialManagement.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface IExpenseService
    {
        Task<IEnumerable<ExpenseViewModel>> GetAllExpensesAsync();
        Task<ExpenseViewModel?> GetExpenseByIdAsync(int id);
        Task<ExpenseViewModel> CreateExpenseAsync(ExpenseCreateModel model);
        Task<ExpenseViewModel> UpdateExpenseAsync(ExpenseUpdateModel model);
        Task DeleteExpenseAsync(int id);

        // <<< أضف هاتين الدالتين >>>
        Task<decimal> GetTotalExpensesAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<ExpenseViewModel>> GetExpensesByDateRangeAsync(DateTime startDate, DateTime endDate);

        IQueryable<Expense> GetExpensesAsQueryable();
    }
}