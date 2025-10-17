using MaterialManagement.BLL.ModelVM.Material;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using AutoMapper;
namespace MaterialManagement.PL.Controllers
{
    public class MaterialController : Controller
    {
        private readonly IMaterialService _materialService;
        private readonly IMapper _mapper;
        public MaterialController(IMaterialService materialService, IMapper mapper)
        {
            _materialService = materialService;
            _mapper = mapper;
        }

        [HttpPost]
        public async Task<IActionResult> LoadData()
        {
            try
            {
                // --- 1. قراءة البيانات التي يرسلها الجدول الذكي ---
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault(); // من أي سجل أبدأ
                var length = Request.Form["length"].FirstOrDefault(); // كم سجل أعرض
                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault();
                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault(); // قيمة البحث
                var isActiveFilter = Request.Form["isActiveFilter"].FirstOrDefault(); // قيمة الفلتر المخصص

                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                // --- 2. بناء الاستعلام الديناميكي ---
                IQueryable<Material> query = _materialService.GetMaterialsAsQueryable();

                // أ. الفلترة المخصصة (الحالة: نشط/غير نشط)
                if (!string.IsNullOrEmpty(isActiveFilter))
                {
                    bool isActive = Convert.ToBoolean(isActiveFilter);
                    query = query.Where(m => m.IsActive == isActive);
                }

                // ب. الفلترة العامة (صندوق البحث)
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m => m.Name.Contains(searchValue) || m.Code.Contains(searchValue));
                }

                // ج. الترتيب (Sorting)
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }

                // د. حساب عدد السجلات (بعد الفلترة)
                int recordsFiltered = await query.CountAsync();

                
                List<Material> pagedData = await query.Skip(skip).Take(pageSize).ToListAsync();

                
                var viewModelData = _mapper.Map<IEnumerable<MaterialViewModel>>(pagedData);

                // --- 3. إرسال الرد بالصيغة التي يفهمها الجدول الذكي ---
                int recordsTotal = await _materialService.GetMaterialsAsQueryable().CountAsync();
                var jsonData = new { draw = draw, recordsFiltered = recordsFiltered, recordsTotal = recordsTotal, data = viewModelData };

                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                // يمكنك تسجيل الخطأ هنا
                return BadRequest(new { error = ex.Message });
            }
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