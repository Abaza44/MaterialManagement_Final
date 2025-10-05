using MaterialManagement.BLL.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;
        private readonly IClientService _clientService;
        private readonly ISupplierService _supplierService;
        private readonly IMaterialService _materialService;

        public ReportController(
            IReportService reportService,
            IClientService clientService,
            ISupplierService supplierService,
            IMaterialService materialService)
        {
            _reportService = reportService;
            _clientService = clientService;
            _supplierService = supplierService;
            _materialService = materialService;
        }

        // === Account Statement Report ===

        // GET: /Report/AccountStatement
        public async Task<IActionResult> AccountStatement()
        {
            ViewBag.Clients = new SelectList(await _clientService.GetAllClientsAsync(), "Id", "Name");
            ViewBag.Suppliers = new SelectList(await _supplierService.GetAllSuppliersAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> AccountStatement(int? clientId, int? supplierId, DateTime fromDate, DateTime toDate)
        {
            if (clientId.HasValue && clientId > 0)
            {
                var statement = await _reportService.GetClientAccountStatementAsync(clientId.Value, fromDate, toDate);

                // <<< التعديل هنا: نمرر الكائن بأكمله >>>
                ViewBag.AccountHolder = await _clientService.GetClientByIdAsync(clientId.Value);

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                return View("AccountStatementResult", statement);
            }

            if (supplierId.HasValue && supplierId > 0)
            {
                var statement = await _reportService.GetSupplierAccountStatementAsync(supplierId.Value, fromDate, toDate);

                // <<< التعديل هنا: نمرر الكائن بأكمله >>>
                ViewBag.AccountHolder = await _supplierService.GetSupplierByIdAsync(supplierId.Value);

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                // سنحتاج إلى View منفصل أو منطق إضافي في View واحد. لنستخدم نفس الـ View حاليًا.
                return View("AccountStatementResult", statement);
            }

            TempData["Error"] = "يرجى تحديد عميل أو مورد للبحث.";
            return RedirectToAction(nameof(AccountStatement));
        }

        // === Material Movement Report ===

        // GET: /Report/MaterialMovement
        public async Task<IActionResult> MaterialMovement()
        {
            ViewBag.Materials = new SelectList(await _materialService.GetAllMaterialsAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> MaterialMovement(int materialId, DateTime fromDate, DateTime toDate)
        {
            if (materialId <= 0)
            {
                TempData["Error"] = "يرجى تحديد مادة.";
                return RedirectToAction(nameof(MaterialMovement));
            }

            var reportData = await _reportService.GetMaterialMovementAsync(materialId, fromDate, toDate);
            var material = await _materialService.GetMaterialByIdAsync(materialId);
            ViewBag.MaterialName = material?.Name;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;

            return View("MaterialMovementResult", reportData);
        }

        // === Profit Report ===

        // GET: /Report/ProfitReport
        public IActionResult ProfitReport()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProfitReport(DateTime fromDate, DateTime toDate)
        {
            var reportData = await _reportService.GetProfitReportAsync(fromDate, toDate);
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            return View("ProfitReportResult", reportData);
        }
    }
}