using MaterialManagement.BLL.ModelVM.Maintenance;
using MaterialManagement.BLL.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    // نحدد Route أساسي للـ Controller بأكمله
    [Route("Maintenance")]
    public class MaintenanceController : Controller
    {
        private readonly IMaintenanceService _maintenanceService;
        private readonly IEquipmentService _equipmentService;

        public MaintenanceController(IMaintenanceService maintenanceService, IEquipmentService equipmentService)
        {
            _maintenanceService = maintenanceService;
            _equipmentService = equipmentService;
        }

        // GET: /Maintenance/Index  أو  /Maintenance
        [HttpGet]
        [Route("")] // يطابق الرابط الأساسي للـ Controller
        [Route("Index")]
        public async Task<IActionResult> Index()
        {
            var allEquipment = await _equipmentService.GetAllEquipmentAsync();
            var allMaintenanceRecords = allEquipment
                .SelectMany(e => e.MaintenanceHistory)
                .OrderByDescending(r => r.MaintenanceDate)
                .ToList();
            return View(allMaintenanceRecords);
        }

        // GET: /Maintenance/Add
        // هذه الدالة تعرض الفورم العام لاختيار المعدة
        [HttpGet]
        [Route("Add")] // يطابق الرابط /Maintenance/Add فقط
        public async Task<IActionResult> Add()
        {
            var allEquipment = await _equipmentService.GetAllEquipmentAsync();
            ViewBag.EquipmentList = new SelectList(allEquipment, "Code", "Name");

            var model = new MaintenanceRecordCreateModel();
            return View(model);
        }

        // GET: /Maintenance/AddForEquipment/{equipmentCode}
        // هذه الدالة تعرض الفورم لمعدة محددة
        [HttpGet]
        [Route("AddForEquipment/{equipmentCode:int}")] // يطابق رابطًا مختلفًا + يتأكد أن equipmentCode هو رقم
        public async Task<IActionResult> AddForEquipment(int equipmentCode)
        {
            var equipment = await _equipmentService.GetByCodeAsync(equipmentCode);
            if (equipment == null)
            {
                return NotFound("المعدة غير موجودة");
            }

            var model = new MaintenanceRecordCreateModel { EquipmentCode = equipmentCode };
            ViewBag.EquipmentName = equipment.Name;

            // نطلب منه استخدام نفس الـ View الخاص بـ Add
            return View("Add", model);
        }

        // POST: /Maintenance/Add
        // هذه الدالة تستقبل البيانات من كلا الفورمين
        [HttpPost]
        [Route("Add")] // يطابق /Maintenance/Add مع طلب POST
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(MaintenanceRecordCreateModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _maintenanceService.AddMaintenanceRecordAsync(model);
                    TempData["Success"] = "تم تسجيل عملية الصيانة بنجاح.";
                    return RedirectToAction("Details", "Equipment", new { code = model.EquipmentCode });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"حدث خطأ: {ex.Message}");
                }
            }

            // في حالة وجود خطأ، يجب إعادة تحميل البيانات اللازمة للـ View
            if (model.EquipmentCode == 0) // هذا يعني أننا كنا في الفورم العام
            {
                var allEquipment = await _equipmentService.GetAllEquipmentAsync();
                ViewBag.EquipmentList = new SelectList(allEquipment, "Code", "Name", model.EquipmentCode);
            }
            else // كنا في الفورم المحدد
            {
                var equipment = await _equipmentService.GetByCodeAsync(model.EquipmentCode);
                ViewBag.EquipmentName = equipment?.Name;
            }

            return View(model);
        }
    }
}