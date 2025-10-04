using MaterialManagement.BLL.ModelVM.Payment;
using MaterialManagement.BLL.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IClientPaymentService _clientPaymentService;
        private readonly ISupplierPaymentService _supplierPaymentService;
        private readonly IClientService _clientService;
        private readonly ISupplierService _supplierService;
        private readonly ISalesInvoiceService _salesInvoiceService;
        private readonly IPurchaseInvoiceService _purchaseInvoiceService;

        public PaymentController(
            IClientPaymentService clientPaymentService,
            ISupplierPaymentService supplierPaymentService,
            IClientService clientService,
            ISupplierService supplierService,
            ISalesInvoiceService salesInvoiceService,
            IPurchaseInvoiceService purchaseInvoiceService)
        {
            _clientPaymentService = clientPaymentService;
            _supplierPaymentService = supplierPaymentService;
            _clientService = clientService;
            _supplierService = supplierService;
            _salesInvoiceService = salesInvoiceService;
            _purchaseInvoiceService = purchaseInvoiceService;
        }

        // === Client Payments (التحصيلات) ===

        // GET: /Payment/AddClientPayment
        public async Task<IActionResult> AddClientPayment(int? clientId, int? invoiceId)
        {
            var model = new ClientPaymentCreateModel();

            // Get all clients for dropdown
            var clients = await _clientService.GetAllClientsAsync();
            ViewBag.ClientList = new SelectList(clients, "Id", "Name", clientId);

            if (clientId.HasValue)
            {
                model.ClientId = clientId.Value;
                // Get unpaid invoices for this client
                var invoices = await _salesInvoiceService.GetUnpaidInvoicesForClientAsync(clientId.Value);
                ViewBag.InvoiceList = new SelectList(invoices, "Id", "InvoiceNumber", invoiceId);
            }

            if (invoiceId.HasValue)
            {
                model.SalesInvoiceId = invoiceId.Value;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddClientPayment(ClientPaymentCreateModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _clientPaymentService.AddPaymentAsync(model);
                    TempData["Success"] = "تم تسجيل التحصيل بنجاح.";
                    return RedirectToAction("Details", "Client", new { id = model.ClientId });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"حدث خطأ: {ex.Message}");
                }
            }

            // Reload dropdown lists if model is invalid
            var clients = await _clientService.GetAllClientsAsync();
            ViewBag.ClientList = new SelectList(clients, "Id", "Name", model.ClientId);
            if (model.ClientId > 0)
            {
                var invoices = await _salesInvoiceService.GetUnpaidInvoicesForClientAsync(model.ClientId);
                ViewBag.InvoiceList = new SelectList(invoices, "Id", "InvoiceNumber", model.SalesInvoiceId);
            }
            return View(model);
        }

        // === Supplier Payments (التوريدات) ===

        // GET: /Payment/AddSupplierPayment
        public async Task<IActionResult> AddSupplierPayment(int? supplierId, int? invoiceId)
        {
            var model = new SupplierPaymentCreateModel();

            var suppliers = await _supplierService.GetAllSuppliersAsync();
            ViewBag.SupplierList = new SelectList(suppliers, "Id", "Name", supplierId);

            if (supplierId.HasValue)
            {
                model.SupplierId = supplierId.Value;
                var invoices = await _purchaseInvoiceService.GetUnpaidInvoicesForSupplierAsync(supplierId.Value);
                ViewBag.InvoiceList = new SelectList(invoices, "Id", "InvoiceNumber", invoiceId);
            }

            if (invoiceId.HasValue)
            {
                model.PurchaseInvoiceId = invoiceId.Value;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSupplierPayment(SupplierPaymentCreateModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _supplierPaymentService.AddPaymentAsync(model);
                    TempData["Success"] = "تم تسجيل الدفعة للمورد بنجاح.";
                    return RedirectToAction("Details", "Supplier", new { id = model.SupplierId });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"حدث خطأ: {ex.Message}");
                }
            }

            var suppliers = await _supplierService.GetAllSuppliersAsync();
            ViewBag.SupplierList = new SelectList(suppliers, "Id", "Name", model.SupplierId);
            if (model.SupplierId > 0)
            {
                var invoices = await _purchaseInvoiceService.GetUnpaidInvoicesForSupplierAsync(model.SupplierId);
                ViewBag.InvoiceList = new SelectList(invoices, "Id", "InvoiceNumber", model.PurchaseInvoiceId);
            }
            return View(model);
        }
        // API Endpoint to get unpaid invoices for a client
        // GET: /Payment/GetUnpaidInvoicesForClient/5
        [HttpGet]
        public async Task<IActionResult> GetUnpaidInvoicesForClient(int id)
        {
            var invoices = await _salesInvoiceService.GetUnpaidInvoicesForClientAsync(id);
            // نرجع البيانات بصيغة JSON ليقرأها الـ JavaScript
            return Json(invoices.Select(i => new { id = i.Id, invoiceNumber = i.InvoiceNumber }));
        }

        // API Endpoint to get unpaid invoices for a supplier
        // GET: /Payment/GetUnpaidInvoicesForSupplier/5
        [HttpGet]
        public async Task<IActionResult> GetUnpaidInvoicesForSupplier(int id)
        {
            var invoices = await _purchaseInvoiceService.GetUnpaidInvoicesForSupplierAsync(id);
            return Json(invoices.Select(i => new { id = i.Id, invoiceNumber = i.InvoiceNumber }));
        }
    }
}