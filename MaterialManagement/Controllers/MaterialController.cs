using Microsoft.AspNetCore.Mvc;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.BLL.ModelVM.Material;

namespace MaterialManagement.PL.Controllers
{
    public class MaterialController : Controller
    {
        private readonly IMaterialService _materialService;

        public MaterialController(IMaterialService materialService)
        {
            _materialService = materialService;
        }

        // GET: Material
        public async Task<IActionResult> Index(string searchTerm)
        {
            try
            {
                IEnumerable<MaterialViewModel> materials;

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    materials = await _materialService.SearchMaterialsAsync(searchTerm);
                    ViewBag.SearchTerm = searchTerm;
                }
                else
                {
                    materials = await _materialService.GetAllMaterialsAsync();
                }

                return View(materials);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات: " + ex.Message;
                return View(new List<MaterialViewModel>());
            }
        }

        // GET: Material/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var material = await _materialService.GetMaterialByIdAsync(id);
                if (material == null)
                {
                    TempData["ErrorMessage"] = "المادة غير موجودة";
                    return RedirectToAction(nameof(Index));
                }

                return View(material);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "حدث خطأ: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Material/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Material/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MaterialCreateModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await _materialService.CreateMaterialAsync(model);
                TempData["SuccessMessage"] = "تم إضافة المادة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return View(model);
            }
        }

        // GET: Material/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var material = await _materialService.GetMaterialByIdAsync(id);
                if (material == null)
                {
                    TempData["ErrorMessage"] = "المادة غير موجودة";
                    return RedirectToAction(nameof(Index));
                }

                var model = new MaterialUpdateModel
                {
                    Name = material.Name,
                    Code = material.Code,
                    Unit = material.Unit,
                    Quantity = material.Quantity,
                    Description = material.Description,
                    IsActive = material.IsActive
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "حدث خطأ: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Material/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MaterialUpdateModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await _materialService.UpdateMaterialAsync(id, model);
                TempData["SuccessMessage"] = "تم تحديث المادة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return View(model);
            }
        }

        // GET: Material/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var material = await _materialService.GetMaterialByIdAsync(id);
                if (material == null)
                {
                    TempData["ErrorMessage"] = "المادة غير موجودة";
                    return RedirectToAction(nameof(Index));
                }

                return View(material);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "حدث خطأ: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Material/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _materialService.DeleteMaterialAsync(id);
                TempData["SuccessMessage"] = "تم حذف المادة بنجاح";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Material/LowStock
        public async Task<IActionResult> LowStock()
        {
            try
            {
                var lowStockMaterials = await _materialService.GetLowStockMaterialsAsync();
                return View(lowStockMaterials);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات: " + ex.Message;
                return View(new List<MaterialViewModel>());
            }
        }
    }
}