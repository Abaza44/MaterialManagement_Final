using MaterialManagement.BLL.ModelVM.Employee;
using MaterialManagement.DAL.Entities;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface IEmployeeService
    {
        Task<IEnumerable<EmployeeViewModel>> GetAllEmployeesAsync();
        Task<EmployeeViewModel?> GetEmployeeByIdAsync(int id);
        Task<EmployeeViewModel> CreateEmployeeAsync(EmployeeCreateModel model);
        Task<EmployeeViewModel> UpdateEmployeeAsync(EmployeeUpdateModel model);
        Task DeleteEmployeeAsync(int id);

        IQueryable<Employee> GetEmployeesAsQueryable();
    }
}