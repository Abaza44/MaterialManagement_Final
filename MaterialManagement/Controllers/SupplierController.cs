using MaterialManagement.BLL.ModelVM.Supplier;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities; 
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.AspNetCore.Mvc;
using System; 
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
namespace MaterialManagement.PL.Controllers
{
    public class SupplierController : Controller
    {
        private readonly ISupplierService _supplierService;
        private readonly ISupplierPaymentService _supplierPaymentService;
        private readonly IMapper _mapper;
        public SupplierController(ISupplierService supplierService, ISupplierPaymentService supplierPaymentService, IMapper mapper)
        {
            _supplierService = supplierService;
            _supplierPaymentService = supplierPaymentService;
            _mapper = mapper;
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


            var payments = await _supplierPaymentService.GetPaymentsForSupplierAsync(id); 
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
        [HttpPost]
        public async Task<IActionResult> LoadData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Request.Form["start"].FirstOrDefault();
            var length = Request.Form["length"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault();

            // فلتر مخصص للموردين الذين لهم مستحقات
            var hasCreditFilter = Request.Form["hasCreditFilter"].FirstOrDefault();

            int pageSize = length != null ? Convert.ToInt32(length) : 10;
            int skip = start != null ? Convert.ToInt32(start) : 0;

            IQueryable<Supplier> query = _supplierService.GetSuppliersAsQueryable();

            
            if (!string.IsNullOrEmpty(hasCreditFilter) && Convert.ToBoolean(hasCreditFilter))
            {
                query = query.Where(s => s.Balance > 0);
            }

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(s => s.Name.Contains(searchValue) || (s.Phone != null && s.Phone.Contains(searchValue)));
            }

            var recordsFiltered = await query.CountAsync();
            var pagedData = await query.Skip(skip).Take(pageSize).ToListAsync();
            var viewModelData = _mapper.Map<IEnumerable<SupplierViewModel>>(pagedData);
            var recordsTotal = await _supplierService.GetSuppliersAsQueryable().CountAsync();

            var jsonData = new { draw = draw, recordsFiltered = recordsFiltered, recordsTotal = recordsTotal, data = viewModelData };
            return Ok(jsonData);
        }
    }
}