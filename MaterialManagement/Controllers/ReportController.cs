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

        [HttpGet]
        public IActionResult AccountStatement()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AccountStatement(int? clientId, int? supplierId, DateTime fromDate, DateTime toDate)
        {
            if (clientId.HasValue && clientId > 0)
            {
                var statement = await _reportService.GetClientAccountStatementAsync(clientId.Value, fromDate, toDate);
                ViewBag.AccountHolder = await _clientService.GetClientByIdAsync(clientId.Value);
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                return View("AccountStatementResult", statement);
            }

            if (supplierId.HasValue && supplierId > 0)
            {
                var statement = await _reportService.GetSupplierAccountStatementAsync(supplierId.Value, fromDate, toDate);
                ViewBag.AccountHolder = await _supplierService.GetSupplierByIdAsync(supplierId.Value);
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                return View("AccountStatementResult", statement);
            }

            TempData["Error"] = "يرجى تحديد عميل أو مورد للبحث.";
            return RedirectToAction(nameof(AccountStatement));
        }

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

        [HttpGet]
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

            ViewBag.MaterialId = materialId;

            return View("MaterialMovementResult", reportData);
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
            var reportData = await _reportService.GetProfitReportAsync(fromDate, toDate);
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            return View("ProfitReportResult", reportData);
        }

        [HttpGet]
        public async Task<IActionResult> AccountStatementResult(int? clientId, int? supplierId)
        {
            if (!clientId.HasValue && !supplierId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد عميل أو مورد.";
                return RedirectToAction(nameof(AccountStatement));
            }

            // 1. تحديد نوع الحساب
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

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> LoadAccountStatementData(int accountId, bool isClient, DateTime? fromDate, DateTime? toDate)
        {
            try
            {


                List<AccountStatementViewModel> statementData;

                if (isClient)
                {
                    statementData = await _reportService.GetClientAccountStatementAsync(
                        accountId,
                        fromDate,
                        toDate);
                }
                else
                {
                    statementData = await _reportService.GetSupplierAccountStatementAsync(
                        accountId,
                        fromDate,
                        toDate);
                }

                if (!fromDate.HasValue && !toDate.HasValue)
                {


                    statementData = statementData

                        .OrderByDescending(d => d.TransactionDate)

                        .Take(10)

                        .OrderBy(d => d.TransactionDate)
                        .ToList();
                }



                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();

                var pageSize = length != null ? Convert.ToInt32(length) : 10;
                var skip = start != null ? Convert.ToInt32(start) : 0;

                // الإجمالي قبل التصفية (لأننا طبقنا فلترة الـ 10 سجلات بالفعل، نستخدم عدد السجلات بعد الفلترة)
                var totalRecords = statementData.Count();

                // تطبيق الترقيم النهائي
                var displayedData = statementData.Skip(skip).Take(pageSize).ToList();

                // 4. إرجاع الرد
                var jsonData = new
                {
                    draw = draw,
                    recordsFiltered = totalRecords,
                    recordsTotal = totalRecords,
                    data = displayedData,
                    // يجب عليك إرجاع الإجماليات والرصيد النهائي ليعرضها الجدول (كما اتفقنا في الخطوة السابقة)
                    totalDebit = statementData.Sum(i => i.Debit),
                    totalCredit = statementData.Sum(i => i.Credit),
                    finalBalance = statementData.LastOrDefault()?.Balance ?? 0
                };

                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                // ...
                return StatusCode(500, new { error = $"حدث خطأ غير متوقع: {ex.Message}" });
            }
        }
        [HttpPost]
        public async Task<IActionResult> LoadMaterialMovementData()
        {
            try
            {

                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var materialId = int.Parse(Request.Form["materialId"].FirstOrDefault()); // معرّف المادة


                DateTime? fromDate = null;
                if (DateTime.TryParse(Request.Form["fromDate"].FirstOrDefault(), out DateTime tempFrom)) { fromDate = tempFrom; }

                DateTime? toDate = null;
                if (DateTime.TryParse(Request.Form["toDate"].FirstOrDefault(), out DateTime tempTo)) { toDate = tempTo; }

                // 2. جلب البيانات من الـ Service
                var reportData = await _reportService.GetMaterialMovementAsync(materialId, fromDate, toDate);


                var totalRecords = reportData.Count;

                var pageSize = length != null ? Convert.ToInt32(length) : 10;
                var skip = start != null ? Convert.ToInt32(start) : 0;

                var displayedData = reportData.Skip(skip).Take(pageSize).ToList();

                // 4. حساب الإجماليات والرصيد النهائي
                var totalIn = reportData.Sum(i => i.QuantityIn);
                var totalOut = reportData.Sum(i => i.QuantityOut);
                var finalBalance = reportData.LastOrDefault()?.Balance ?? 0;

                // 5. إرسال الرد
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
    }
}
