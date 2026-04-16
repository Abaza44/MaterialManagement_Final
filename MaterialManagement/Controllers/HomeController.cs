using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Enums;
using MaterialManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly MaterialManagementContext _context;

        public HomeController(
            ILogger<HomeController> logger,
            MaterialManagementContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var dashboard = await BuildDashboardAsync(DateTime.Now);
                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading the dashboard.");
                TempData["ErrorMessage"] = "حدث خطأ أثناء تحميل لوحة التحكم.";
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        private async Task<ErpDashboardViewModel> BuildDashboardAsync(DateTime now)
        {
            var today = now.Date;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var last7Days = today.AddDays(-6);

            var activeClients = _context.Clients.AsNoTracking().Where(c => c.IsActive);
            var activeSuppliers = _context.Suppliers.AsNoTracking().Where(s => s.IsActive);
            var activeMaterials = _context.Materials.AsNoTracking().Where(m => m.IsActive);
            var activeSalesInvoices = _context.SalesInvoices.AsNoTracking().Where(i => i.IsActive);
            var activePurchaseInvoices = _context.PurchaseInvoices.AsNoTracking().Where(i => i.IsActive);
            var activeExpenses = _context.Expenses.AsNoTracking().Where(e => e.IsActive);
            var activeReservations = _context.Reservations.AsNoTracking().Where(r => r.Status == ReservationStatus.Active);

            var materialRows = await activeMaterials
                .Select(m => new DashboardMaterialItem
                {
                    Id = m.Id,
                    Code = m.Code,
                    Name = m.Name,
                    Unit = m.Unit,
                    Quantity = m.Quantity,
                    ReservedQuantity = m.ReservedQuantity,
                    AvailableQuantity = m.Quantity - m.ReservedQuantity,
                    Url = "/Material/Details/" + m.Id
                })
                .ToListAsync();

            var dashboard = new ErpDashboardViewModel
            {
                GeneratedAt = now,
                MonthStart = monthStart,
                Today = today,
                ClientReceivables = await activeClients.Where(c => c.Balance > 0).SumAsync(c => c.Balance),
                ClientCreditBalances = await activeClients.Where(c => c.Balance < 0).SumAsync(c => -c.Balance),
                SupplierPayables = await activeSuppliers.Where(s => s.Balance > 0).SumAsync(s => s.Balance),
                SupplierCreditBalances = await activeSuppliers.Where(s => s.Balance < 0).SumAsync(s => -s.Balance),
                EstimatedStockValue = await activeMaterials.SumAsync(m => m.Quantity * (m.PurchasePrice ?? 0m)),
                NegativeStockCount = materialRows.Count(m => m.Quantity < 0),
                UnavailableStockCount = materialRows.Count(m => m.AvailableQuantity <= 0),
                OpenReservationsCount = await activeReservations.CountAsync(),
                OpenReservationsValue = await activeReservations.SumAsync(r => r.TotalAmount),
                UnpaidSalesInvoiceCount = await activeSalesInvoices.CountAsync(i => i.RemainingAmount > 0),
                UnpaidSalesInvoiceAmount = await activeSalesInvoices.Where(i => i.RemainingAmount > 0).SumAsync(i => i.RemainingAmount),
                UnpaidPurchaseInvoiceCount = await activePurchaseInvoices.CountAsync(i => i.RemainingAmount > 0),
                UnpaidPurchaseInvoiceAmount = await activePurchaseInvoices.Where(i => i.RemainingAmount > 0).SumAsync(i => i.RemainingAmount),
                MonthSalesTotal = await activeSalesInvoices.Where(i => i.InvoiceDate >= monthStart).SumAsync(i => i.TotalAmount),
                MonthPurchaseTotal = await activePurchaseInvoices.Where(i => i.InvoiceDate >= monthStart).SumAsync(i => i.TotalAmount),
                MonthExpenseTotal = await activeExpenses.Where(e => e.ExpenseDate >= monthStart).SumAsync(e => e.Amount),
                MonthSalesReturnTotal = await _context.SalesReturns.AsNoTracking()
                    .Where(r => r.IsActive && r.Status == ReturnStatus.Posted && r.ReturnDate >= monthStart)
                    .SumAsync(r => r.TotalNetAmount),
                Last7DaysSalesTotal = await activeSalesInvoices.Where(i => i.InvoiceDate >= last7Days).SumAsync(i => i.TotalAmount),
                Last7DaysPurchaseTotal = await activePurchaseInvoices.Where(i => i.InvoiceDate >= last7Days).SumAsync(i => i.TotalAmount)
            };

            dashboard.TopClientBalances = await activeClients
                .Where(c => c.Balance > 0)
                .OrderByDescending(c => c.Balance)
                .Take(6)
                .Select(c => new DashboardBalanceItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Detail = c.Phone ?? "بدون هاتف",
                    Balance = c.Balance,
                    Url = "/Client/Details/" + c.Id
                })
                .ToListAsync();

            dashboard.TopSupplierBalances = await activeSuppliers
                .Where(s => s.Balance > 0)
                .OrderByDescending(s => s.Balance)
                .Take(6)
                .Select(s => new DashboardBalanceItem
                {
                    Id = s.Id,
                    Name = s.Name,
                    Detail = s.Phone ?? "بدون هاتف",
                    Balance = s.Balance,
                    Url = "/Supplier/Details/" + s.Id
                })
                .ToListAsync();

            dashboard.MaterialsNeedingReview = materialRows
                .Where(m => m.Quantity < 0 || m.AvailableQuantity <= 0)
                .OrderBy(m => m.AvailableQuantity)
                .ThenBy(m => m.Quantity)
                .Take(8)
                .ToList();

            dashboard.PendingReservations = await activeReservations
                .OrderBy(r => r.ReservationDate)
                .Take(6)
                .Select(r => new DashboardReservationItem
                {
                    Id = r.Id,
                    ReservationNumber = r.ReservationNumber,
                    ClientName = r.Client.Name,
                    ReservationDate = r.ReservationDate,
                    TotalAmount = r.TotalAmount,
                    Url = "/Reservation/Details/" + r.Id
                })
                .ToListAsync();

            dashboard.AttentionItems = await BuildAttentionItemsAsync(dashboard, today);
            dashboard.RecentActivities = await BuildRecentActivitiesAsync();

            return dashboard;
        }

        private async Task<List<DashboardAttentionItem>> BuildAttentionItemsAsync(ErpDashboardViewModel dashboard, DateTime today)
        {
            var items = new List<DashboardAttentionItem>();

            if (dashboard.NegativeStockCount > 0)
            {
                items.Add(new DashboardAttentionItem
                {
                    Title = "مخزون سالب",
                    Detail = "مواد كميتها الحالية أقل من صفر وتحتاج مراجعة حركة المخزون.",
                    Value = dashboard.NegativeStockCount.ToString("N0"),
                    Tone = "danger",
                    Url = "/Material/Index"
                });
            }

            if (dashboard.UnavailableStockCount > 0)
            {
                items.Add(new DashboardAttentionItem
                {
                    Title = "مواد غير متاحة",
                    Detail = "مواد رصيدها المتاح صفر أو أقل بعد خصم الحجوزات.",
                    Value = dashboard.UnavailableStockCount.ToString("N0"),
                    Tone = "warning",
                    Url = "/Material/LowStock"
                });
            }

            if (dashboard.OpenReservationsCount > 0)
            {
                items.Add(new DashboardAttentionItem
                {
                    Title = "حجوزات مفتوحة",
                    Detail = "حجوزات لم يتم تسليمها أو إلغاؤها بعد.",
                    Value = dashboard.OpenReservationsCount.ToString("N0"),
                    Tone = "info",
                    Url = "/Reservation/Index"
                });
            }

            if (dashboard.UnpaidSalesInvoiceCount > 0)
            {
                items.Add(new DashboardAttentionItem
                {
                    Title = "فواتير بيع غير مسددة",
                    Detail = "فواتير عليها متبقي مطلوب من العملاء المسجلين.",
                    Value = dashboard.UnpaidSalesInvoiceAmount.ToString("N2"),
                    Tone = "danger",
                    Url = "/SalesInvoice/Index"
                });
            }

            if (dashboard.UnpaidPurchaseInvoiceCount > 0)
            {
                items.Add(new DashboardAttentionItem
                {
                    Title = "فواتير شراء غير مسددة",
                    Detail = "عمليات شراء عليها متبقي للموردين.",
                    Value = dashboard.UnpaidPurchaseInvoiceAmount.ToString("N2"),
                    Tone = "warning",
                    Url = "/PurchaseInvoice/Index"
                });
            }

            var maintenanceReviewCount = await _context.Equipment
                .AsNoTracking()
                .CountAsync(e => !e.MaintenanceHistory.Any()
                    || e.MaintenanceHistory.Max(m => m.MaintenanceDate) <= today.AddDays(-90));

            if (maintenanceReviewCount > 0)
            {
                items.Add(new DashboardAttentionItem
                {
                    Title = "مراجعة صيانة معدات",
                    Detail = "معدات بلا سجل صيانة أو آخر صيانة لها منذ أكثر من 90 يوم.",
                    Value = maintenanceReviewCount.ToString("N0"),
                    Tone = "warning",
                    Url = "/Equipment/Report"
                });
            }

            return items
                .OrderBy(i => i.Tone == "danger" ? 0 : i.Tone == "warning" ? 1 : 2)
                .Take(8)
                .ToList();
        }

        private async Task<List<DashboardActivityItem>> BuildRecentActivitiesAsync()
        {
            var activities = new List<DashboardActivityItem>();

            activities.AddRange(await _context.SalesInvoices.AsNoTracking()
                .Where(i => i.IsActive)
                .OrderByDescending(i => i.InvoiceDate)
                .Take(8)
                .Select(i => new DashboardActivityItem
                {
                    Date = i.InvoiceDate,
                    Type = "فاتورة بيع",
                    Reference = i.InvoiceNumber,
                    Party = i.Client != null ? i.Client.Name : (i.OneTimeCustomerName ?? "عميل نقدي"),
                    Amount = i.TotalAmount,
                    Tone = "debit",
                    Url = "/SalesInvoice/Details/" + i.Id
                })
                .ToListAsync());

            activities.AddRange(await _context.PurchaseInvoices.AsNoTracking()
                .Where(i => i.IsActive)
                .OrderByDescending(i => i.InvoiceDate)
                .Take(8)
                .Select(i => new DashboardActivityItem
                {
                    Date = i.InvoiceDate,
                    Type = i.PartyMode == PurchaseInvoicePartyMode.RegisteredClientReturn ? "مرتجع عميل" : "فاتورة شراء",
                    Reference = i.InvoiceNumber,
                    Party = i.Supplier != null ? i.Supplier.Name : (i.OneTimeSupplierName ?? (i.Client != null ? i.Client.Name : "طرف غير محدد")),
                    Amount = i.TotalAmount,
                    Tone = i.PartyMode == PurchaseInvoicePartyMode.RegisteredClientReturn ? "credit" : "warning",
                    Url = "/PurchaseInvoice/Details/" + i.Id
                })
                .ToListAsync());

            activities.AddRange(await _context.ClientPayments.AsNoTracking()
                .OrderByDescending(p => p.PaymentDate)
                .Take(8)
                .Select(p => new DashboardActivityItem
                {
                    Date = p.PaymentDate,
                    Type = "تحصيل عميل",
                    Reference = p.SalesInvoice != null ? p.SalesInvoice.InvoiceNumber : "دفعة تحت الحساب",
                    Party = p.Client.Name,
                    Amount = p.Amount,
                    Tone = "credit",
                    Url = "/Client/Details/" + p.ClientId
                })
                .ToListAsync());

            activities.AddRange(await _context.SupplierPayments.AsNoTracking()
                .OrderByDescending(p => p.PaymentDate)
                .Take(8)
                .Select(p => new DashboardActivityItem
                {
                    Date = p.PaymentDate,
                    Type = "دفعة مورد",
                    Reference = p.PurchaseInvoice != null ? p.PurchaseInvoice.InvoiceNumber : "دفعة تحت الحساب",
                    Party = p.Supplier.Name,
                    Amount = p.Amount,
                    Tone = "warning",
                    Url = "/Supplier/Details/" + p.SupplierId
                })
                .ToListAsync());

            activities.AddRange(await _context.SalesReturns.AsNoTracking()
                .Where(r => r.IsActive && r.Status == ReturnStatus.Posted)
                .OrderByDescending(r => r.ReturnDate)
                .Take(8)
                .Select(r => new DashboardActivityItem
                {
                    Date = r.ReturnDate,
                    Type = "مرتجع بيع",
                    Reference = r.ReturnNumber,
                    Party = r.Client.Name,
                    Amount = r.TotalNetAmount,
                    Tone = "credit",
                    Url = "/SalesInvoice/Details/" + r.SalesInvoiceId
                })
                .ToListAsync());

            activities.AddRange(await _context.Reservations.AsNoTracking()
                .OrderByDescending(r => r.ReservationDate)
                .Take(8)
                .Select(r => new DashboardActivityItem
                {
                    Date = r.ReservationDate,
                    Type = "حجز",
                    Reference = r.ReservationNumber,
                    Party = r.Client.Name,
                    Amount = r.TotalAmount,
                    Tone = r.Status == ReservationStatus.Active ? "info" : "neutral",
                    Url = "/Reservation/Details/" + r.Id
                })
                .ToListAsync());

            activities.AddRange(await _context.Expenses.AsNoTracking()
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.ExpenseDate)
                .Take(8)
                .Select(e => new DashboardActivityItem
                {
                    Date = e.ExpenseDate,
                    Type = "مصروف",
                    Reference = e.Category ?? "مصروف",
                    Party = e.PaymentTo ?? (e.Employee != null ? e.Employee.Name : e.Description),
                    Amount = e.Amount,
                    Tone = "warning",
                    Url = "/Expense/Details/" + e.Id
                })
                .ToListAsync());

            activities.AddRange(await _context.MaintenanceRecords.AsNoTracking()
                .OrderByDescending(m => m.MaintenanceDate)
                .Take(8)
                .Select(m => new DashboardActivityItem
                {
                    Date = m.MaintenanceDate,
                    Type = "صيانة",
                    Reference = "معدة " + m.EquipmentCode,
                    Party = m.Equipment.Name,
                    Amount = m.Cost,
                    Tone = "neutral",
                    Url = "/Equipment/Details/" + m.EquipmentCode
                })
                .ToListAsync());

            return activities
                .OrderByDescending(a => a.Date)
                .Take(14)
                .ToList();
        }

        // This is the Landing Page
        public IActionResult Landing()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet("Home/Expense")]
        [HttpGet("Home/Expense/Index")]
        public IActionResult LegacyExpenseIndex()
        {
            return RedirectToAction("Index", "Expense");
        }

        [HttpGet("Home/Expense/Create")]
        public IActionResult LegacyExpenseCreate()
        {
            return RedirectToAction("Create", "Expense");
        }

        [HttpGet("Home/Expense/Edit/{id:int?}")]
        public IActionResult LegacyExpenseEdit(int? id)
        {
            return id.HasValue
                ? RedirectToAction("Edit", "Expense", new { id = id.Value })
                : RedirectToAction("Index", "Expense");
        }

        [HttpGet("Home/Expense/Details/{id:int?}")]
        public IActionResult LegacyExpenseDetails(int? id)
        {
            return id.HasValue
                ? RedirectToAction("Details", "Expense", new { id = id.Value })
                : RedirectToAction("Index", "Expense");
        }

        [HttpGet("Home/Expense/Delete/{id:int?}")]
        public IActionResult LegacyExpenseDelete(int? id)
        {
            return id.HasValue
                ? RedirectToAction("Delete", "Expense", new { id = id.Value })
                : RedirectToAction("Index", "Expense");
        }

        [HttpGet("Home/Expense/Report")]
        public IActionResult LegacyExpenseReport()
        {
            return RedirectToAction("Report", "Expense");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
