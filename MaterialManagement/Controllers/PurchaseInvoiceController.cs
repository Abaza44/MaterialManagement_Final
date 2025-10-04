using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Repo.Abstractions; // <-- مهم
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq; // <-- مهم
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    public class PurchaseInvoiceController : Controller
    {
        private readonly IPurchaseInvoiceService _purchaseInvoiceService;
        private readonly ISupplierService _supplierService;
        private readonly IMaterialService _materialService;
        private readonly ISupplierPaymentRepo _supplierPaymentRepo; // <<< تم إضافته هنا

        // <<< تم تحديث الـ Constructor >>>
        public PurchaseInvoiceController(
            IPurchaseInvoiceService purchaseInvoiceService,
            ISupplierService supplierService,
            IMaterialService materialService,
            ISupplierPaymentRepo supplierPaymentRepo) // <<< تم إضافته هنا
        {
            _purchaseInvoiceService = purchaseInvoiceService;
            _supplierService = supplierService;
            _materialService = materialService;
            _supplierPaymentRepo = supplierPaymentRepo; // <<< تم إضافته هنا
        }

        // GET: PurchaseInvoice
        public async Task<IActionResult> Index()
        {
            var invoices = await _purchaseInvoiceService.GetAllInvoicesAsync();
            return View(invoices);
        }

        // GET: PurchaseInvoice/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _purchaseInvoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "الفاتورة غير موجودة";
                return RedirectToAction(nameof(Index));

            }

            // <<< جلب سجل الدفعات المرتبط بهذه الفاتورة >>>
            var payments = await _supplierPaymentRepo.GetByInvoiceIdAsync(id);
            ViewBag.Payments = payments;

            return View(invoice);
        }

        // GET: PurchaseInvoice/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Suppliers = await _supplierService.GetAllSuppliersAsync();
            ViewBag.Materials = await _materialService.GetAllMaterialsAsync();

            var model = new PurchaseInvoiceCreateModel
            {
                Items = new List<PurchaseInvoiceItemCreateModel> { new PurchaseInvoiceItemCreateModel() }
            };

            return View(model);
        }

        // POST: PurchaseInvoice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseInvoiceCreateModel model)
        {
            // إزالة البنود الفارغة التي قد يرسلها الفورم
            model.Items.RemoveAll(i => i.Quantity == 0 || i.UnitPrice == 0);

            if (!ModelState.IsValid || !model.Items.Any())
            {
                if (!model.Items.Any())
                {
                    ModelState.AddModelError("Items", "يجب إضافة بند واحد على الأقل للفاتورة.");
                }
                ViewBag.Suppliers = await _supplierService.GetAllSuppliersAsync();
                ViewBag.Materials = await _materialService.GetAllMaterialsAsync();
                return View(model);
            }

            try
            {
                await _purchaseInvoiceService.CreateInvoiceAsync(model);
                TempData["SuccessMessage"] = "تم إنشاء فاتورة شراء بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                ViewBag.Suppliers = await _supplierService.GetAllSuppliersAsync();
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
    }
}