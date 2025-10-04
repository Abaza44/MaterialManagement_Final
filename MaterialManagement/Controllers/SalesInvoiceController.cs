using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Repo.Abstractions; // <-- مهم
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    public class SalesInvoiceController : Controller
    {
        private readonly ISalesInvoiceService _salesInvoiceService;
        private readonly IClientService _clientService;
        private readonly IMaterialService _materialService;
        private readonly IClientPaymentRepo _clientPaymentRepo; // <<< تم إضافته هنا

        // <<< تم تحديث الـ Constructor >>>
        public SalesInvoiceController(
            ISalesInvoiceService salesInvoiceService,
            IClientService clientService,
            IMaterialService materialService,
            IClientPaymentRepo clientPaymentRepo) // <<< تم إضافته هنا
        {
            _salesInvoiceService = salesInvoiceService;
            _clientService = clientService;
            _materialService = materialService;
            _clientPaymentRepo = clientPaymentRepo; // <<< تم إضافته هنا
        }

        // GET: SalesInvoice
        public async Task<IActionResult> Index()
        {
            var invoices = await _salesInvoiceService.GetAllInvoicesAsync();
            return View(invoices);
        }

        // GET: SalesInvoice/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _salesInvoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "الفاتورة غير موجودة";
                return RedirectToAction(nameof(Index));
            }

            // <<< جلب سجل الدفعات المرتبط بهذه الفاتورة >>>
            var payments = await _clientPaymentRepo.GetByInvoiceIdAsync(id);
            ViewBag.Payments = payments;

            return View(invoice);
        }

        // GET: SalesInvoice/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Clients = await _clientService.GetAllClientsAsync();
            ViewBag.Materials = await _materialService.GetAllMaterialsAsync();

            var model = new SalesInvoiceCreateModel
            {
                Items = new List<SalesInvoiceItemCreateModel> { new SalesInvoiceItemCreateModel() }
            };

            return View(model);
        }

        // POST: SalesInvoice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesInvoiceCreateModel model)
        {
            // إزالة البنود الفارغة التي قد يرسلها الفورم
            model.Items.RemoveAll(i => i.Quantity == 0 || i.UnitPrice == 0);

            if (!ModelState.IsValid || !model.Items.Any())
            {
                if (!model.Items.Any())
                {
                    ModelState.AddModelError("Items", "يجب إضافة بند واحد على الأقل للفاتورة.");
                }
                ViewBag.Clients = await _clientService.GetAllClientsAsync();
                ViewBag.Materials = await _materialService.GetAllMaterialsAsync();
                return View(model);
            }

            try
            {
                await _salesInvoiceService.CreateInvoiceAsync(model);
                TempData["SuccessMessage"] = "تم إنشاء فاتورة بيع بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                ViewBag.Clients = await _clientService.GetAllClientsAsync();
                ViewBag.Materials = await _materialService.GetAllMaterialsAsync();
                return View(model);
            }
        }

        // GET: SalesInvoice/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var invoice = await _salesInvoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "الفاتورة غير موجودة";
                return RedirectToAction(nameof(Index));
            }
            return View(invoice);
        }

        // POST: SalesInvoice/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _salesInvoiceService.DeleteInvoiceAsync(id);
                TempData["SuccessMessage"] = "تم حذف الفاتورة بنجاح";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}