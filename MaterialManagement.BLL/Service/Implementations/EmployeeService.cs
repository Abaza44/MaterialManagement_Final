using AutoMapper;
using MaterialManagement.BLL.ModelVM.Employee;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class EmployeeService : IEmployeeService
    {
        private readonly IEmployeeRepo _employeeRepo;
        private readonly IMapper _mapper;
        public EmployeeService(IEmployeeRepo employeeRepo, IMapper mapper) { _employeeRepo = employeeRepo; _mapper = mapper; }

        public async Task<IEnumerable<EmployeeViewModel>> GetAllEmployeesAsync() => _mapper.Map<IEnumerable<EmployeeViewModel>>(await _employeeRepo.GetAllAsync());
        public async Task<EmployeeViewModel?> GetEmployeeByIdAsync(int id) => _mapper.Map<EmployeeViewModel>(await _employeeRepo.GetByIdAsync(id));

        public async Task<EmployeeViewModel> CreateEmployeeAsync(EmployeeCreateModel model)
        {
            var employee = _mapper.Map<Employee>(model);
            var created = await _employeeRepo.CreateAsync(employee);
            return _mapper.Map<EmployeeViewModel>(created);
        }

        public async Task<EmployeeViewModel> UpdateEmployeeAsync(EmployeeUpdateModel model)
        {
            var employee = await _employeeRepo.GetByIdAsync(model.Id);
            if (employee == null) throw new Exception("الموظف غير موجود");

            _mapper.Map(model, employee);
            var updated = await _employeeRepo.UpdateAsync(employee);
            return _mapper.Map<EmployeeViewModel>(updated);
        }

        public async Task DeleteEmployeeAsync(int id) => await _employeeRepo.DeleteAsync(id);
    }
}