using AutoMapper;
using MaterialManagement.BLL.ModelVM.Expense;
using MaterialManagement.BLL.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    public class ExpenseController : Controller
    {
        private readonly IExpenseService _expenseService;
        private readonly IEmployeeService _employeeService; // <-- تم إضافته
        private readonly IMapper _mapper; // <-- تم إضافته

        public ExpenseController(
            IExpenseService expenseService,
            IEmployeeService employeeService, // <-- تم إضافته
            IMapper mapper) // <-- تم إضافته
        {
            _expenseService = expenseService;
            _employeeService = employeeService; // <-- تم إضافته
            _mapper = mapper; // <-- تم إضافته
        }

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
        {
            IEnumerable<ExpenseViewModel> expenses;
            if (startDate.HasValue && endDate.HasValue)
            {
                expenses = await _expenseService.GetExpensesByDateRangeAsync(startDate.Value, endDate.Value);
                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            }
            else
            {
                expenses = await _expenseService.GetAllExpensesAsync();
            }
            ViewBag.TotalExpenses = await _expenseService.GetTotalExpensesAsync(startDate, endDate);
            return View(expenses);
        }

        // تم حذف Details لأننا لا نحتاجه عادة للمصاريف

        public async Task<IActionResult> Create()
        {
            // تمرير قائمة الموظفين للـ View
            ViewBag.Employees = new SelectList(await _employeeService.GetAllEmployeesAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExpenseCreateModel model)
        {
            if (ModelState.IsValid)
            {
                await _expenseService.CreateExpenseAsync(model);
                TempData["Success"] = "تم إضافة المصروف بنجاح";
                return RedirectToAction(nameof(Index));
            }
            // إعادة تحميل قائمة الموظفين في حالة وجود خطأ
            ViewBag.Employees = new SelectList(await _employeeService.GetAllEmployeesAsync(), "Id", "Name", model.EmployeeId);
            return View(model);

        }

        public async Task<IActionResult> Edit(int id)
        {
            var expenseViewModel = await _expenseService.GetExpenseByIdAsync(id);
            if (expenseViewModel == null) return NotFound();

            // استخدام AutoMapper للتحويل
            var model = _mapper.Map<ExpenseUpdateModel>(expenseViewModel);

            ViewBag.Employees = new SelectList(await _employeeService.GetAllEmployeesAsync(), "Id", "Name", model.EmployeeId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ExpenseUpdateModel model)
        {
            if (ModelState.IsValid)
            {
                await _expenseService.UpdateExpenseAsync(model);
                TempData["Success"] = "تم تحديث المصروف بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Employees = new SelectList(await _employeeService.GetAllEmployeesAsync(), "Id", "Name", model.EmployeeId);
            return View(model);
        }

        // <<< تم تعديل منطق الحذف بالكامل >>>
        // لم نعد بحاجة لصفحة تأكيد (GET)
        [HttpPost]
        [ValidateAntiForgeryToken]
        
        public async Task<IActionResult> Delete(int id)
        {
            await _expenseService.DeleteExpenseAsync(id);
            TempData["Success"] = "تم حذف المصروف (تعطيل).";
            return RedirectToAction(nameof(Index));
        }

        // --- تقرير المصاريف (لا يحتاج تعديل كبير) ---
        public async Task<IActionResult> Report()
        {
            var currentMonth = DateTime.Now;
            var firstDayOfMonth = new DateTime(currentMonth.Year, currentMonth.Month, 1);

            var monthlyExpenses = await _expenseService.GetExpensesByDateRangeAsync(firstDayOfMonth, currentMonth);
            var totalMonthly = await _expenseService.GetTotalExpensesAsync(firstDayOfMonth, currentMonth);

            if (totalMonthly == 0) // تجنب القسمة على صفر
            {
                ViewBag.CategoryStats = new List<object>();
            }
            else
            {
                var categoryStats = monthlyExpenses.GroupBy(e => e.Category)
                   .Select(g => new {
                       Category = g.Key,
                       Total = g.Sum(e => e.Amount),
                       Percentage = (g.Sum(e => e.Amount) / totalMonthly) * 100
                   }).OrderByDescending(x => x.Total).ToList();
                ViewBag.CategoryStats = categoryStats;
            }

            ViewBag.MonthlyTotal = totalMonthly;
            return View(monthlyExpenses);
        }
    }
}