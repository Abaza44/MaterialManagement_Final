using AutoMapper;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.ModelVM.Payment;
using MaterialManagement.BLL.ModelVM.Supplier;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Enums;
using MaterialManagement.PL.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaterialManagement.PL.Controllers
{
    public class PurchaseInvoiceController : Controller
    {
        private readonly IPurchaseInvoiceService _purchaseInvoiceService;
        private readonly ISupplierService _supplierService;
        private readonly IClientService _clientService;
        private readonly IMaterialService _materialService;
        private readonly ISupplierPaymentService _supplierPaymentService;
        private readonly IMapper _mapper;
        private readonly ISupervisorAuthorizationService _supervisorAuthorizationService;

        public PurchaseInvoiceController(
            IPurchaseInvoiceService purchaseInvoiceService,
            ISupplierService supplierService,
            IClientService clientService,
            IMaterialService materialService,
            ISupplierPaymentService supplierPaymentService,
            IMapper mapper,
            ISupervisorAuthorizationService supervisorAuthorizationService)
        {
            _purchaseInvoiceService = purchaseInvoiceService;
            _supplierService = supplierService;
            _clientService = clientService;
            _materialService = materialService;
            _supplierPaymentService = supplierPaymentService;
            _mapper = mapper;
            _supervisorAuthorizationService = supervisorAuthorizationService;
        }


        public async Task<IActionResult> Index()
        {
            var supplierSummaries = await _purchaseInvoiceService.GetSupplierInvoiceSummariesAsync();
            return View(supplierSummaries);
        }


        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _purchaseInvoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "الفاتورة غير موجودة";
                return RedirectToAction(nameof(Index));
            }

            var payments = new List<SupplierPaymentViewModel>();
            if (invoice.SupplierId.HasValue)
            {
                payments = (await _supplierPaymentService.GetPaymentsForInvoiceAsync(id)).ToList();
            }

            var viewModel = new PurchaseInvoiceDetailsViewModel
            {
                Invoice = invoice,
                Payments = payments
            };

            return View(viewModel);
        }


        [HttpGet]
        public async Task<IActionResult> SupplierInvoices(int id)
        {
            if (id == 0)
            {
                return View(new SupplierViewModel
                {
                    Id = 0,
                    Name = "موردون يدويون / بدون تسجيل",
                    Balance = 0,
                    IsActive = true
                });
            }

            var supplier = await _supplierService.GetSupplierByIdAsync(id);
            if (supplier == null) return NotFound();
            return View(_mapper.Map<SupplierViewModel>(supplier));
        }
        public async Task<IActionResult> Create()
        {
            ViewBag.Suppliers = await _supplierService.GetAllSuppliersAsync();
            ViewBag.Clients = await _clientService.GetAllClientsAsync();
            ViewBag.Materials = await _materialService.GetAllMaterialsAsync();
            var model = new PurchaseInvoiceCreateModel { Items = new List<PurchaseInvoiceItemCreateModel> { new PurchaseInvoiceItemCreateModel() } };
            return View(model);
        }

        // POST: PurchaseInvoice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseInvoiceCreateModel model, string? supervisorPassword)
        {
            model.Items.RemoveAll(i => i.Quantity == 0 || i.UnitPrice == 0);

            if (!ModelState.IsValid || !model.Items.Any())
            {
                if (!model.Items.Any())
                {
                    ModelState.AddModelError("Items", "يجب إضافة بند واحد على الأقل.");
                }

                ViewBag.Suppliers = await _supplierService.GetAllSuppliersAsync();
                ViewBag.Clients = await _clientService.GetAllClientsAsync();
                ViewBag.Materials = await _materialService.GetAllMaterialsAsync();
                return View(model);
            }

            if (model.PartyMode == PurchaseInvoicePartyMode.RegisteredClientReturn &&
                !_supervisorAuthorizationService.TryAuthorize(supervisorPassword, out var supervisorError))
            {
                ModelState.AddModelError("SupervisorPassword", supervisorError);
                ViewBag.Suppliers = await _supplierService.GetAllSuppliersAsync();
                ViewBag.Clients = await _clientService.GetAllClientsAsync();
                ViewBag.Materials = await _materialService.GetAllMaterialsAsync();
                return View(model);
            }

            try
            {
                var invoice = await _purchaseInvoiceService.CreateInvoiceAsync(model);
                TempData["SuccessMessage"] = "تم إنشاء العملية بنجاح";
                return RedirectToAction(nameof(Details), new { id = invoice.Id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                ViewBag.Suppliers = await _supplierService.GetAllSuppliersAsync();
                ViewBag.Clients = await _clientService.GetAllClientsAsync();
                ViewBag.Materials = await _materialService.GetAllMaterialsAsync();
                return View(model);
            }
        }

        // GET: PurchaseInvoice/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var invoice = await _purchaseInvoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "الفاتورة غير موجودة";
                return RedirectToAction(nameof(Index));
            }
            return View(invoice);
        }

        // POST: PurchaseInvoice/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string? supervisorPassword)
        {
            var invoice = await _purchaseInvoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "الفاتورة غير موجودة";
                return RedirectToAction(nameof(Index));
            }

            if (!_supervisorAuthorizationService.TryAuthorize(supervisorPassword, out var supervisorError))
            {
                ModelState.AddModelError("SupervisorPassword", supervisorError);
                return View(invoice);
            }

            try
            {
                await _purchaseInvoiceService.DeleteInvoiceAsync(id);
                TempData["SuccessMessage"] = "تم حذف الفاتورة بنجاح";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
        // في PurchaseInvoiceController.cs
        [HttpPost]
        public async Task<IActionResult> LoadSupplierInvoices()
        {
            try
            {
                // 1. قراءة متغيرات DataTables
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();
                var supplierIdStr = Request.Form["supplierId"].FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;
                if (!int.TryParse(supplierIdStr, out int supplierId))
                {
                    return BadRequest(new { error = "معرف المورد غير صالح." });
                }


                IQueryable<PurchaseInvoice> query = _purchaseInvoiceService.GetInvoicesAsQueryable()
                    .Where(i => i.IsActive);

                query = supplierId == 0
                    ? query.Where(i => i.PartyMode == PurchaseInvoicePartyMode.OneTimeSupplier)
                    : query.Where(i => i.PartyMode == PurchaseInvoicePartyMode.RegisteredSupplier && i.SupplierId == supplierId);

                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(i => i.InvoiceNumber.Contains(searchValue));
                }

                var grandTotalRemaining = await query.SumAsync(i => i.RemainingAmount);

                var recordsFiltered = await query.CountAsync();
                var pagedData = await query.OrderByDescending(i => i.InvoiceDate).Skip(skip).Take(pageSize).ToListAsync();
                var viewModelData = _mapper.Map<IEnumerable<PurchaseInvoiceViewModel>>(pagedData);

                // 5. جلب الرصيد الإجمالي للمورد وحساب الرصيد المرحل
                var supplier = supplierId == 0 ? null : await _supplierService.GetSupplierByIdAsync(supplierId);
                decimal supplierCurrentBalance = supplier?.Balance ?? 0;
                // الرصيد المرحل = الرصيد النهائي - إجمالي المتبقي من الفواتير
                decimal openingBalance = supplierCurrentBalance - grandTotalRemaining;

                var recordsTotalQuery = _purchaseInvoiceService.GetInvoicesAsQueryable().Where(i => i.IsActive);
                recordsTotalQuery = supplierId == 0
                    ? recordsTotalQuery.Where(i => i.PartyMode == PurchaseInvoicePartyMode.OneTimeSupplier)
                    : recordsTotalQuery.Where(i => i.PartyMode == PurchaseInvoicePartyMode.RegisteredSupplier && i.SupplierId == supplierId);
                var recordsTotal = await recordsTotalQuery.CountAsync();

                // 6. إرسال الرد مع كل الإحصائيات المطلوبة
                var jsonData = new
                {
                    draw = draw,
                    recordsFiltered = recordsFiltered,
                    recordsTotal = recordsTotal,
                    data = viewModelData,
                    openingBalance = openingBalance.ToString("N2"), // <<< الرصيد المرحل
                    grandTotalRemaining = grandTotalRemaining.ToString("N2"), // إجمالي المتبقي من الفواتير
                    finalBalance = supplierCurrentBalance.ToString("N2") // الرصيد النهائي
                };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "حدث خطأ في جلب بيانات الفواتير: " + ex.Message });
            }
        }
        [HttpPost]
        public async Task<IActionResult> LoadData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Request.Form["start"].FirstOrDefault();
            var length = Request.Form["length"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault();
            int pageSize = length != null ? Convert.ToInt32(length) : 10;
            int skip = start != null ? Convert.ToInt32(start) : 0;

            IQueryable<PurchaseInvoice> query = _purchaseInvoiceService.GetInvoicesAsQueryable()
                                                    .Where(i => i.IsActive);

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(i => i.InvoiceNumber.Contains(searchValue)
                                       || (i.Supplier != null && i.Supplier.Name.Contains(searchValue))
                                       || (i.OneTimeSupplierName != null && i.OneTimeSupplierName.Contains(searchValue))
                                       || (i.Client != null && i.Client.Name.Contains(searchValue)));
            }

            var recordsFiltered = await query.CountAsync();
            var pagedData = await query.OrderByDescending(i => i.InvoiceDate).Skip(skip).Take(pageSize).ToListAsync();
            var viewModelData = _mapper.Map<IEnumerable<PurchaseInvoiceViewModel>>(pagedData);
            var recordsTotal = await _purchaseInvoiceService.GetInvoicesAsQueryable().Where(i => i.IsActive).CountAsync();

            var jsonData = new { draw = draw, recordsFiltered = recordsFiltered, recordsTotal = recordsTotal, data = viewModelData };
            return Ok(jsonData);
        }
    }
}
