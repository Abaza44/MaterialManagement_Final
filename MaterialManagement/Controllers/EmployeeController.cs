using AutoMapper;
using MaterialManagement.BLL.ModelVM.Employee;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaterialManagement.PL.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly IEmployeeService _employeeService;
        private readonly IMapper _mapper;

        public EmployeeController(IEmployeeService employeeService, IMapper mapper)
        {
            _employeeService = employeeService;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _employeeService.GetAllEmployeesAsync());
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeCreateModel model)
        {
            if (ModelState.IsValid)
            {
                await _employeeService.CreateEmployeeAsync(model);
                TempData["Success"] = "تم إضافة الموظف بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _employeeService.GetEmployeeByIdAsync(id);
            if (vm == null) return NotFound();
            var model = _mapper.Map<EmployeeUpdateModel>(vm);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EmployeeUpdateModel model)
        {
            if (ModelState.IsValid)
            {
                await _employeeService.UpdateEmployeeAsync(model);
                TempData["Success"] = "تم تحديث بيانات الموظف بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _employeeService.DeleteEmployeeAsync(id);
            TempData["Success"] = "تم تعطيل الموظف.";
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

                IQueryable<Employee> query = _employeeService.GetEmployeesAsQueryable();

                // Apply filtering (Search)
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(e => e.Name.Contains(searchValue) || (e.Phone != null && e.Phone.Contains(searchValue)) || (e.Position != null && e.Position.Contains(searchValue)));
                }

                var recordsFiltered = await query.CountAsync();
                var pagedData = await query.Skip(skip).Take(pageSize).ToListAsync();
                var viewModelData = _mapper.Map<IEnumerable<EmployeeViewModel>>(pagedData);
                var recordsTotal = await _employeeService.GetEmployeesAsQueryable().CountAsync();

                var jsonData = new { draw = draw, recordsFiltered = recordsFiltered, recordsTotal = recordsTotal, data = viewModelData };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return BadRequest();
            }
        }
    }
}