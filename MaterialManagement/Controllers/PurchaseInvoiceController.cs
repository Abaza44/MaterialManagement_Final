using AutoMapper;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.ModelVM.Payment;
using MaterialManagement.BLL.ModelVM.Supplier;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities;
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

        public PurchaseInvoiceController(
            IPurchaseInvoiceService purchaseInvoiceService,
            ISupplierService supplierService,
            IClientService clientService,
            IMaterialService materialService,
            ISupplierPaymentService supplierPaymentService,
            IMapper mapper)
        {
            _purchaseInvoiceService = purchaseInvoiceService;
            _supplierService = supplierService;
            _clientService = clientService;
            _materialService = materialService;
            _supplierPaymentService = supplierPaymentService;
            _mapper = mapper;
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
            var supplier = await _supplierService.GetSupplierByIdAsync(id);
            if (supplier == null) return NotFound();
            return View(new SupplierViewModel { Id = supplier.Id, Name = supplier.Name });
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
        public async Task<IActionResult> Create(PurchaseInvoiceCreateModel model)
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

            try
            {
                await _purchaseInvoiceService.CreateInvoiceAsync(model);
                TempData["SuccessMessage"] = "تم إنشاء العملية بنجاح"; 
                return RedirectToAction(nameof(Index));
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
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
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
        [HttpPost]
        public async Task<IActionResult> LoadSupplierInvoices()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Request.Form["start"].FirstOrDefault();
            var length = Request.Form["length"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault();
            var supplierId = int.Parse(Request.Form["supplierId"].FirstOrDefault());

            int pageSize = length != null ? Convert.ToInt32(length) : 10;
            int skip = start != null ? Convert.ToInt32(start) : 0;

            IQueryable<PurchaseInvoice> query = _purchaseInvoiceService.GetInvoicesAsQueryable()
                                               .Where(i => i.SupplierId == supplierId && i.IsActive);

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(i => i.InvoiceNumber.Contains(searchValue));
            }

            var recordsFiltered = await query.CountAsync();
            var pagedData = await query.OrderByDescending(i => i.InvoiceDate).Skip(skip).Take(pageSize).ToListAsync();
            var viewModelData = _mapper.Map<IEnumerable<PurchaseInvoiceViewModel>>(pagedData);
            var recordsTotal = await _purchaseInvoiceService.GetInvoicesAsQueryable().Where(i => i.SupplierId == supplierId && i.IsActive).CountAsync();

            var jsonData = new { draw = draw, recordsFiltered = recordsFiltered, recordsTotal = recordsTotal, data = viewModelData };
            return Ok(jsonData);
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