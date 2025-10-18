using AutoMapper;
using MaterialManagement.BLL.ModelVM.Expense;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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

        [HttpPost]
        public async Task<IActionResult> LoadData()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();

                // <<< الفلتر المخصص الجديد >>>
                var categoryFilter = Request.Form["categoryFilter"].FirstOrDefault();
                var startDateFilter = Request.Form["startDate"].FirstOrDefault();
                var endDateFilter = Request.Form["endDate"].FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                IQueryable<Expense> query = _expenseService.GetExpensesAsQueryable()
                                               .Where(e => e.IsActive); // فقط المصاريف النشطة

                // أ. الفلترة حسب الفئة
                if (!string.IsNullOrEmpty(categoryFilter))
                {
                    query = query.Where(e => e.Category == categoryFilter);
                }

                // ب. الفلترة حسب التاريخ
                if (!string.IsNullOrEmpty(startDateFilter) && DateTime.TryParse(startDateFilter, out DateTime startDate))
                {
                    query = query.Where(e => e.ExpenseDate >= startDate.Date);
                }
                if (!string.IsNullOrEmpty(endDateFilter) && DateTime.TryParse(endDateFilter, out DateTime endDate))
                {
                    // ليشمل اليوم بالكامل
                    var inclusiveEndDate = endDate.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(e => e.ExpenseDate <= inclusiveEndDate);
                }

                // ج. الفلترة العامة (صندوق البحث)
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(e => e.Description.Contains(searchValue)
                                           || e.Category.Contains(searchValue)
                                           || (e.PaymentTo != null && e.PaymentTo.Contains(searchValue)));
                }

                // د. حساب الإجمالي الكلي للمصاريف (للفلاتر المطبقة)
                var totalAmountFiltered = await query.SumAsync(e => e.Amount);

                // هـ. الترتيب (نستخدم الترتيب الافتراضي حسب التاريخ)
                query = query.OrderByDescending(e => e.ExpenseDate);

                // و. تطبيق الترقيم
                var recordsFiltered = await query.CountAsync();
                var pagedData = await query.Skip(skip).Take(pageSize).ToListAsync();

                // ز. تحويل البيانات
                var viewModelData = _mapper.Map<IEnumerable<ExpenseViewModel>>(pagedData);

                var recordsTotal = await _expenseService.GetExpensesAsQueryable().Where(e => e.IsActive).CountAsync();

                var jsonData = new
                {
                    draw = draw,
                    recordsFiltered = recordsFiltered,
                    recordsTotal = recordsTotal,
                    data = viewModelData,
                    // تمرير الإجمالي المحسوب كإحصائية إضافية
                    totalFilteredAmount = totalAmountFiltered.ToString("N2")
                };

                return Ok(jsonData);
            }
            catch (Exception)
            {
                return BadRequest();
            }
        }
    }
}