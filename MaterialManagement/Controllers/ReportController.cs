using MaterialManagement.BLL.ModelVM.Reports;
using MaterialManagement.BLL.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;

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

        // ==========================================
        // 🔹 1) Account Statement Report (كشف الحساب)
        // ==========================================

        // (صفحة البحث)
        [HttpGet]
        public IActionResult AccountStatement()
        {
            return View();
        }

        // (صفحة عرض النتيجة - التي تحتوي على DataTables)
        [HttpGet]
        public async Task<IActionResult> AccountStatementResult(int? clientId, int? supplierId)
        {
            if (!clientId.HasValue && !supplierId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد عميل أو مورد.";
                return RedirectToAction(nameof(AccountStatement));
            }

            var isClient = clientId.HasValue;
            int accountId = isClient ? clientId.Value : supplierId.Value;

            if (isClient)
            {
                ViewBag.AccountHolder = await _clientService.GetClientByIdAsync(accountId);
            }
            else
            {
                ViewBag.AccountHolder = await _supplierService.GetSupplierByIdAsync(accountId);
            }

            ViewBag.IsClient = isClient;
            ViewBag.AccountId = accountId;

            return View(); // (هذه الصفحة ستقوم بطلب AJAX)
        }

        // (الدالة التي يستدعيها AJAX لجلب بيانات كشف الحساب)
        [HttpGet]
        public async Task<IActionResult> LoadAccountStatementData(int accountId, bool isClient, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var statementData = isClient
                    ? await _reportService.GetClientAccountStatementAsync(accountId, fromDate, toDate)
                    : await _reportService.GetSupplierAccountStatementAsync(accountId, fromDate, toDate);

                if (statementData == null)
                    statementData = new List<AccountStatementViewModel>();

                var draw = Request.Query["draw"].FirstOrDefault();
                var start = Convert.ToInt32(Request.Query["start"].FirstOrDefault() ?? "0");
                var length = Convert.ToInt32(Request.Query["length"].FirstOrDefault() ?? "10");

                var totalRecords = statementData.Count;
                var totalDebit = statementData.Sum(i => i.Debit);
                var totalCredit = statementData.Sum(i => i.Credit);
                var finalBalance = statementData.LastOrDefault()?.Balance ?? 0;
                var openingBalance = statementData.FirstOrDefault(t => t.TransactionType.Contains("افتتاحي"))?.Balance ?? 0;

                var displayedData = statementData.Skip(start).Take(length).ToList();

                return Json(new
                {
                    draw,
                    recordsFiltered = totalRecords,
                    recordsTotal = totalRecords,
                    data = displayedData.Select(x => new
                    {
                        transactionDate = x.TransactionDate,
                        transactionType = x.TransactionType,
                        debit = x.Debit,
                        credit = x.Credit,
                        balance = x.Balance,
                        reference = x.Reference,
                        documentId = x.DocumentId,
                        documentType = x.DocumentType,
                        items = x.Items?.Select(i => new
                        {
                            materialName = i.MaterialName,
                            quantity = i.Quantity,
                            unit = i.Unit,
                            price = i.UnitPrice
                        }).ToList()
                    }),
                    totalDebit = totalDebit.ToString("N2"),
                    totalCredit = totalCredit.ToString("N2"),
                    finalBalance = finalBalance.ToString("N2"),
                    openingBalance = openingBalance.ToString("N2")
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = $"حدث خطأ غير متوقع: {ex.Message}" });
            }
        }

        // (دوال مساعدة للبحث في صفحة كشف الحساب)
        public async Task<IActionResult> SearchClients(string searchTerm)
        {
            var clients = await _clientService.SearchClientsAsync(searchTerm);
            var results = clients.Select(c => new
            {
                id = c.Id,
                text = $"{c.Name} ({c.Phone})"
            }).ToList();

            return Json(new { results = results });
        }

        public async Task<IActionResult> SearchSuppliers(string searchTerm)
        {
            var suppliers = await _supplierService.SearchSuppliersAsync(searchTerm);
            var results = suppliers.Select(s => new
            {
                id = s.Id,
                text = $"{s.Name} ({s.Phone})"
            }).ToList();

            return Json(new { results = results });
        }

        // ==========================================
        // 🔹 2) Material Movement Report (حركة المواد)
        // ==========================================

        // (صفحة البحث)
        [HttpGet]
        public async Task<IActionResult> MaterialMovement()
        {
            ViewBag.Materials = new SelectList(await _materialService.GetAllMaterialsAsync(), "Id", "Name");
            return View();
        }

        // (صفحة عرض النتيجة - التي تحتوي على DataTables)
        [HttpGet]
        public async Task<IActionResult> MaterialMovementResult(int materialId, DateTime? fromDate, DateTime? toDate)
        {
            if (materialId <= 0)
            {
                TempData["Error"] = "يرجى تحديد مادة.";
                return RedirectToAction(nameof(MaterialMovement));
            }

            var material = await _materialService.GetMaterialByIdAsync(materialId);
            if (material == null)
            {
                TempData["Error"] = "المادة المحددة غير موجودة.";
                return RedirectToAction(nameof(MaterialMovement));
            }

            ViewBag.MaterialName = material.Name;
            ViewBag.MaterialId = materialId;
            // (تمرير التواريخ للـ View ليستخدمها في طلب الـ AJAX)
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(); // (هذه الصفحة ستقوم بطلب AJAX)
        }

        // (الدالة التي يستدعيها AJAX لجلب بيانات حركة المواد)
        [HttpGet] // (الإصلاح 1: تغييرها إلى Get)
        public async Task<IActionResult> LoadMaterialMovementData(int materialId, DateTime? fromDate, DateTime? toDate) // (الإصلاح 2: استقبال المتغيرات هنا)
        {
            try
            {
                // (الإصلاح 3: القراءة من Request.Query بدلاً من Request.Form)
                var draw = Request.Query["draw"].FirstOrDefault();
                var start = Request.Query["start"].FirstOrDefault();
                var length = Request.Query["length"].FirstOrDefault();

                // (لم نعد بحاجة لعمل Parse للمتغيرات لأنها جاءت في الدالة)

                var reportData = await _reportService.GetMaterialMovementAsync(materialId, fromDate, toDate);

                var totalRecords = reportData.Count;

                var pageSize = length != null ? Convert.ToInt32(length) : 10;
                var skip = start != null ? Convert.ToInt32(start) : 0;

                var displayedData = reportData.Skip(skip).Take(pageSize).ToList();

                var totalIn = reportData.Sum(i => i.QuantityIn);
                var totalOut = reportData.Sum(i => i.QuantityOut);
                var finalBalance = reportData.LastOrDefault()?.Balance ?? 0;

                var jsonData = new
                {
                    draw = draw,
                    recordsFiltered = totalRecords,
                    recordsTotal = totalRecords,
                    data = displayedData,
                    totalIn = totalIn.ToString("N2"),
                    totalOut = totalOut.ToString("N2"),
                    finalBalance = finalBalance.ToString("N2")
                };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }


        // ==========================================
        // 🔹 3) Profit Report (تقرير الأرباح)
        // ==========================================

        [HttpGet]
        public IActionResult ProfitReport()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProfitReport(DateTime fromDate, DateTime toDate)
        {
            // (هذه الدالة لا تستخدم AJAX، لذا من الطبيعي أن تكون Post)
            var reportData = await _reportService.GetProfitReportAsync(fromDate, toDate);
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            return View("ProfitReportResult", reportData);
        }
    }
}