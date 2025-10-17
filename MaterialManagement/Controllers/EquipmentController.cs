using AutoMapper;
using MaterialManagement.BLL.ModelVM.Equipment;
using MaterialManagement.BLL.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    public class EquipmentController : Controller
    {
        private readonly IEquipmentService _equipmentService;
        private readonly IMapper _mapper;
        public EquipmentController(IEquipmentService equipmentService,IMapper mapper ) { _equipmentService = equipmentService; _mapper = mapper; }

        public async Task<IActionResult> Index()
        {
            return View();
        }
        [HttpGet("Equipment/Details/{code}")]
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
        [HttpGet("Equipment/Edit/{code}")]
        public async Task<IActionResult> Edit(int code)
        {

            var vm = await _equipmentService.GetByCodeAsync(code);
            if (vm == null)
            {

                return NotFound();
            }
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
        [HttpGet("Equipment/Delete/{code}")]
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

        [HttpPost]
        public async Task<IActionResult> LoadData()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();
                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                IQueryable<EquipmentViewModel> query = _equipmentService.GetEquipmentAsQueryable();

                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(e => e.Name.Contains(searchValue) || e.Code.ToString().Contains(searchValue));
                }

                var recordsFiltered = await query.CountAsync();
                var pagedData = await query.Skip(skip).Take(pageSize).ToListAsync();
                var recordsTotal = await _equipmentService.GetEquipmentAsQueryable().CountAsync();

                var jsonData = new { draw = draw, recordsFiltered = recordsFiltered, recordsTotal = recordsTotal, data = pagedData };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return BadRequest();
            }
        }
    }
}