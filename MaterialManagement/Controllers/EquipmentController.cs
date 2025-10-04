using AutoMapper;
using MaterialManagement.BLL.ModelVM.Equipment;
using MaterialManagement.BLL.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    public class EquipmentController : Controller
    {
        private readonly IEquipmentService _equipmentService;
        private readonly IMapper _mapper;
        public EquipmentController(IEquipmentService equipmentService, IMapper mapper) { _equipmentService = equipmentService; _mapper = mapper; }

        public async Task<IActionResult> Index()
        {
            return View(await _equipmentService.GetAllEquipmentAsync());
        }

        public async Task<IActionResult> Details(int code)
        {
            var equipment = await _equipmentService.GetByCodeAsync(code);
            if (equipment == null) return NotFound();
            return View(equipment);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EquipmentCreateModel model)
        {
            if (ModelState.IsValid)
            {
                await _equipmentService.CreateEquipmentAsync(model);
                TempData["Success"] = "تم إضافة المعدة بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int code)
        {
            var vm = await _equipmentService.GetByCodeAsync(code);
            if (vm == null) return NotFound();
            var model = _mapper.Map<EquipmentUpdateModel>(vm);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EquipmentUpdateModel model)
        {
            if (ModelState.IsValid)
            {
                await _equipmentService.UpdateEquipmentAsync(model);
                TempData["Success"] = "تم تحديث المعدة بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Delete(int code)
        {
            var equipment = await _equipmentService.GetByCodeAsync(code);
            if (equipment == null) return NotFound();
            return View(equipment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int code)
        {
            await _equipmentService.DeleteEquipmentAsync(code);
            TempData["Success"] = "تم حذف المعدة بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}