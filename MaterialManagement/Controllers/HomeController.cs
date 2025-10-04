using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Repo.Abstractions;
using MaterialManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IMaterialService _materialService;
        private readonly IClientService _clientService;
        private readonly ISupplierService _supplierService;
        private readonly ISalesInvoiceService _salesInvoiceService;
        private readonly IPurchaseInvoiceService _purchaseInvoiceService;
        private readonly IEquipmentRepo _equipmentRepo;
        private readonly IExpenseRepo _expenseRepo;
        private readonly IEmployeeRepo _employeeRepo; // <<< تم إضافته

        public HomeController(
            ILogger<HomeController> logger,
            IMaterialService materialService,
            IClientService clientService,
            ISupplierService supplierService,
            ISalesInvoiceService salesInvoiceService,
            IPurchaseInvoiceService purchaseInvoiceService,
            IEquipmentRepo equipmentRepo,
            IExpenseRepo expenseRepo,
            IEmployeeRepo employeeRepo) // <<< تم إضافته
        {
            _logger = logger;
            _materialService = materialService;
            _clientService = clientService;
            _supplierService = supplierService;
            _salesInvoiceService = salesInvoiceService;
            _purchaseInvoiceService = purchaseInvoiceService;
            _equipmentRepo = equipmentRepo;
            _expenseRepo = expenseRepo;
            _employeeRepo = employeeRepo; // <<< تم إضافته
        }

        // This is the Dashboard
        public async Task<IActionResult> Index()
        {
            try
            {
                // Get all data in parallel for better performance
                var materialsTask = _materialService.GetAllMaterialsAsync();
                var clientsTask = _clientService.GetAllClientsAsync();
                var suppliersTask = _supplierService.GetAllSuppliersAsync();
                var salesInvoicesTask = _salesInvoiceService.GetAllInvoicesAsync();
                var purchaseInvoicesTask = _purchaseInvoiceService.GetAllInvoicesAsync();
                var equipmentTask = _equipmentRepo.GetAllAsync();
                var employeesTask = _employeeRepo.GetAllAsync(); // <<< تم إضافته

                await Task.WhenAll(
                    materialsTask, clientsTask, suppliersTask,
                    salesInvoicesTask, purchaseInvoicesTask, equipmentTask, employeesTask);

                var materials = materialsTask.Result;
                var clients = clientsTask.Result;
                var suppliers = suppliersTask.Result;
                var salesInvoices = salesInvoicesTask.Result;
                var purchaseInvoices = purchaseInvoicesTask.Result;
                var equipment = equipmentTask.Result;
                var employees = employeesTask.Result; // <<< تم إضافته

                // --- Calculations ---
                var firstDayOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                // Financials for the current month
                ViewBag.TotalSalesMonth = salesInvoices
                    .Where(i => i.InvoiceDate >= firstDayOfMonth)
                    .Sum(i => i.TotalAmount);

                ViewBag.TotalPurchasesMonth = purchaseInvoices
                    .Where(i => i.InvoiceDate >= firstDayOfMonth)
                    .Sum(i => i.TotalAmount);

                ViewBag.TotalExpensesMonth = await _expenseRepo.GetTotalExpensesAsync(firstDayOfMonth, DateTime.Now);

                // Balances
                ViewBag.TotalClientDebt = clients.Where(c => c.Balance > 0).Sum(c => c.Balance);
                ViewBag.TotalSupplierDebt = suppliers.Where(s => s.Balance > 0).Sum(s => s.Balance);

                // Inventory & Operations
                ViewBag.TotalMaterials = materials.Count();

                // <<< تم إصلاح الشرط المنطقي هنا >>>
                ViewBag.LowStockCount = materials.Count(m => m.Quantity <= m.MinimumQuantity&& m.IsActive);

                ViewBag.TotalEquipment = equipment.Count();
                ViewBag.TotalEmployees = employees.Count(e => e.IsActive); // <<< تم إضافته

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading the dashboard.");
                TempData["ErrorMessage"] = "حدث خطأ أثناء تحميل لوحة التحكم.";
                return View();
            }
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}