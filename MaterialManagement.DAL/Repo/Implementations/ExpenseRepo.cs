using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class ExpenseRepo : IExpenseRepo
    {
        private readonly MaterialManagementContext _context;
        public ExpenseRepo(MaterialManagementContext context) { _context = context; }

        public async Task<IEnumerable<Expense>> GetAllAsync()
        {
            return await _context.Expenses
                .Include(e => e.Employee)
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.ExpenseDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Expense?> GetByIdAsync(int id)
        {
            // نستخدم FindAsync لأنه يبحث بالمفتاح الأساسي وهو الأسرع
            return await _context.Expenses.FindAsync(id);
        }

        public Task CreateAsync(Expense expense)
        {
            _context.Expenses.Add(expense);
            // لا يوجد SaveChangesAsync هنا
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Expense expense)
        {
            // EF Core يتتبع التغييرات تلقائيًا على الكائن الذي تم جلبه للتحديث
            _context.Expenses.Update(expense);
            // لا يوجد SaveChangesAsync هنا
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense != null)
            {
                expense.IsActive = false; // Soft Delete
                // الـ Service هو الذي سيحفظ التغييرات
            }
        }

        // --- دوال التقارير ---

        public async Task<decimal> GetTotalExpensesAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Expenses.AsQueryable();
            if (startDate.HasValue)
            {
                query = query.Where(e => e.ExpenseDate >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                // تعديل ليشمل اليوم بأكمله
                var inclusiveEndDate = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(e => e.ExpenseDate <= inclusiveEndDate);
            }
            return await query.Where(e => e.IsActive).SumAsync(e => e.Amount);
        }

        public async Task<IEnumerable<Expense>> GetExpensesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            var inclusiveEndDate = endDate.Date.AddDays(1).AddTicks(-1);
            return await _context.Expenses
               .Include(e => e.Employee)
               .Where(e => e.IsActive && e.ExpenseDate >= startDate && e.ExpenseDate <= inclusiveEndDate)
               .OrderByDescending(e => e.ExpenseDate)
               .ToListAsync();
        }

        public IQueryable<Expense> GetAsQueryable()
        {
            return _context.Expenses.Include(e => e.Employee).AsQueryable();
        }
    }
}