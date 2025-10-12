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
        private readonly IEquipmentService _equipmentService;
        private readonly IExpenseService _expenseService;
        private readonly IEmployeeService _employeeService;

        public HomeController(
            ILogger<HomeController> logger,
            IMaterialService materialService,
            IClientService clientService,
            ISupplierService supplierService,
            ISalesInvoiceService salesInvoiceService,
            IPurchaseInvoiceService purchaseInvoiceService,
            IEquipmentService equipmentService,
            IExpenseService expenseService,
            IEmployeeService employeeService) // <<< تم إضافته
        {
            _logger = logger;
            _materialService = materialService;
            _clientService = clientService;
            _supplierService = supplierService;
            _salesInvoiceService = salesInvoiceService;
            _purchaseInvoiceService = purchaseInvoiceService;
            _equipmentService = equipmentService;
            _expenseService = expenseService;
            _employeeService = employeeService; // <<< تم إضافته
        }

        // This is the Dashboard
        public async Task<IActionResult> Index()
        {
            try
            {
                // --- Run the queries sequentially ---
                var materials = await _materialService.GetAllMaterialsAsync();
                var clients = await _clientService.GetAllClientsAsync();
                var suppliers = await _supplierService.GetAllSuppliersAsync();
                var salesInvoices = await _salesInvoiceService.GetAllInvoicesAsync();
                var purchaseInvoices = await _purchaseInvoiceService.GetAllInvoicesAsync();
                var equipment = await _equipmentService.GetAllEquipmentAsync(); // Using service
                var employees = await _employeeService.GetAllEmployeesAsync(); // Using service

                // --- Calculations ---
                var firstDayOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                // Financials for the current month
                ViewBag.TotalSalesMonth = salesInvoices
                    .Where(i => i.InvoiceDate >= firstDayOfMonth)
                    .Sum(i => i.TotalAmount);

                ViewBag.TotalPurchasesMonth = purchaseInvoices
                    .Where(i => i.InvoiceDate >= firstDayOfMonth)
                    .Sum(i => i.TotalAmount);

                // 1. Get all expenses
                var allExpenses = await _expenseService.GetAllExpensesAsync();

                // 2. Filter for the current month, then calculate the sum
                ViewBag.TotalExpensesMonth = allExpenses
                    .Where(e => e.ExpenseDate >= firstDayOfMonth) // Assuming your ExpenseViewModel has a 'Date' property
                    .Sum(e => e.Amount);

                // Balances
                ViewBag.TotalClientDebt = clients.Where(c => c.Balance > 0).Sum(c => c.Balance);
                ViewBag.TotalSupplierDebt = suppliers.Where(s => s.Balance > 0).Sum(s => s.Balance);

                // Inventory & Operations
                ViewBag.TotalMaterials = materials.Count();
                ViewBag.LowStockCount = materials.Count(m => m.Quantity <= m.MinimumQuantity && m.IsActive); // Assuming MinimumQuantity exists
                ViewBag.TotalEquipment = equipment.Count();
                ViewBag.TotalEmployees = employees.Count(e => e.IsActive);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading the dashboard.");
                TempData["ErrorMessage"] = "حدث خطأ أثناء تحميل لوحة التحكم.";
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
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