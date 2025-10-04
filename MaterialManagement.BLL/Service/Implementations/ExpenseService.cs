using AutoMapper;
using MaterialManagement.BLL.ModelVM.Expense;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class ExpenseService : IExpenseService
    {
        private readonly MaterialManagementContext _context;
        private readonly IExpenseRepo _expenseRepo;
        private readonly IMapper _mapper;

        public ExpenseService(
            MaterialManagementContext context,
            IExpenseRepo expenseRepo,
            IMapper mapper)
        {
            _context = context;
            _expenseRepo = expenseRepo;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ExpenseViewModel>> GetAllExpensesAsync()
        {
            var expenses = await _expenseRepo.GetAllAsync();
            return _mapper.Map<IEnumerable<ExpenseViewModel>>(expenses);
        }

        public async Task<ExpenseViewModel?> GetExpenseByIdAsync(int id)
        {
            var expense = await _expenseRepo.GetByIdAsync(id);
            return _mapper.Map<ExpenseViewModel>(expense);
        }

        public async Task<ExpenseViewModel> CreateExpenseAsync(ExpenseCreateModel model)
        {
            var expense = _mapper.Map<Expense>(model);
            expense.CreatedDate = DateTime.Now;

            await _expenseRepo.CreateAsync(expense);
            await _context.SaveChangesAsync();

            return _mapper.Map<ExpenseViewModel>(expense);
        }

        public async Task<ExpenseViewModel> UpdateExpenseAsync(ExpenseUpdateModel model)
        {
            // GetByIdAsync لا يستخدم AsNoTracking، لذلك يمكن تحديث الكائن
            var expenseToUpdate = await _expenseRepo.GetByIdAsync(model.Id);
            if (expenseToUpdate == null)
            {
                throw new InvalidOperationException("المصروف غير موجود");
            }

            // استخدم AutoMapper لتحديث كل الخصائص تلقائيًا
            _mapper.Map(model, expenseToUpdate);

            // UpdateAsync لا تحفظ، هي فقط تعلم EF أن الحالة تغيرت
            await _expenseRepo.UpdateAsync(expenseToUpdate);
            await _context.SaveChangesAsync();

            return _mapper.Map<ExpenseViewModel>(expenseToUpdate);
        }

        public async Task DeleteExpenseAsync(int id)
        {
            // DeleteAsync في الـ Repo تقوم فقط بتغيير IsActive
            await _expenseRepo.DeleteAsync(id);
            // نحفظ التغيير هنا
            await _context.SaveChangesAsync();
        }

        // --- دوال التقارير (يجب إضافتها إلى IExpenseService أيضًا) ---

        public async Task<decimal> GetTotalExpensesAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            return await _expenseRepo.GetTotalExpensesAsync(startDate, endDate);
        }

        public async Task<IEnumerable<ExpenseViewModel>> GetExpensesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            var expenses = await _expenseRepo.GetExpensesByDateRangeAsync(startDate, endDate);
            return _mapper.Map<IEnumerable<ExpenseViewModel>>(expenses);
        }
    }
}