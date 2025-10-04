using AutoMapper;
using MaterialManagement.BLL.ModelVM.Employee;
using MaterialManagement.BLL.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;

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
    }
}