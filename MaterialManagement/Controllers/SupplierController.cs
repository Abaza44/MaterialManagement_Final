using MaterialManagement.BLL.ModelVM.Supplier;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities; // <-- أضف هذا
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic; // <-- أضف هذا
using System.Threading.Tasks; // <-- أضف هذا
using System; // <-- أضف هذا

namespace MaterialManagement.PL.Controllers
{
    public class SupplierController : Controller
    {
        private readonly ISupplierService _supplierService;
        private readonly ISupplierPaymentRepo _supplierPaymentRepo; // <<< تم إضافته هنا

        // <<< تم تحديث الـ Constructor هنا >>>
        public SupplierController(ISupplierService supplierService, ISupplierPaymentRepo supplierPaymentRepo)
        {
            _supplierService = supplierService;
            _supplierPaymentRepo = supplierPaymentRepo; // <<< تم إضافته هنا
        }

        // GET: Supplier
        public async Task<IActionResult> Index(string searchTerm)
        {
            IEnumerable<SupplierViewModel> suppliers;
            if (!string.IsNullOrEmpty(searchTerm))
            {
                suppliers = await _supplierService.SearchSuppliersAsync(searchTerm);
                ViewBag.SearchTerm = searchTerm;
            }
            else
            {
                suppliers = await _supplierService.GetAllSuppliersAsync();
            }
            return View(suppliers);
        }

        // GET: Supplier/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var supplier = await _supplierService.GetSupplierByIdAsync(id);
            if (supplier == null)
            {
                TempData["ErrorMessage"] = "المورد غير موجود";
                return RedirectToAction(nameof(Index));
            }

            // <<< الآن هذا الكود سيعمل بشكل صحيح >>>
            var payments = await _supplierPaymentRepo.GetBySupplierIdAsync(id);
            ViewBag.Payments = payments;

            return View(supplier);
        }

        // GET: Supplier/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Supplier/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SupplierCreateModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                await _supplierService.CreateSupplierAsync(model);
                TempData["SuccessMessage"] = "✅ تم إضافة المورد بنجاح"; // تم تصحيح الرسالة
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return View(model);
            }
        }

        // GET: Supplier/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var supplier = await _supplierService.GetSupplierByIdAsync(id);
            if (supplier == null)
            {
                TempData["ErrorMessage"] = "المورد غير موجود";
                return RedirectToAction(nameof(Index));
            }

            var model = new SupplierUpdateModel
            {
                Name = supplier.Name,
                Phone = supplier.Phone,
                Address = supplier.Address,
                Balance = supplier.Balance,
                IsActive = supplier.IsActive
            };
            return View(model);
        }

        // POST: Supplier/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SupplierUpdateModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                await _supplierService.UpdateSupplierAsync(id, model);
                TempData["SuccessMessage"] = "تم تعديل المورد بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return View(model);
            }
        }

        // GET: Supplier/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _supplierService.GetSupplierByIdAsync(id);
            if (supplier == null)
            {
                TempData["ErrorMessage"] = "المورد غير موجود";
                return RedirectToAction(nameof(Index));
            }
            return View(supplier);
        }

        // POST: Supplier/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _supplierService.DeleteSupplierAsync(id);
                TempData["SuccessMessage"] = "تم حذف المورد بنجاح";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Reactivate(int id)
        {
            try
            {
                await _supplierService.ReactivateSupplierAsync(id);
                TempData["SuccessMessage"] = "✅ تم إعادة تفعيل المورد بنجاح";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}