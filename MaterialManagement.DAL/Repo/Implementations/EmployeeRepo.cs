using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class EmployeeRepo : IEmployeeRepo
    {
        private readonly MaterialManagementContext _context;
        public EmployeeRepo(MaterialManagementContext context) { _context = context; }

        public async Task<IEnumerable<Employee>> GetAllAsync() => await _context.Employees.ToListAsync();
        public async Task<Employee?> GetByIdAsync(int id) => await _context.Employees.FindAsync(id);

        public async Task<Employee> CreateAsync(Employee employee)
        {
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
            return employee;
        }

        public async Task<Employee> UpdateAsync(Employee employee)
        {
            _context.Employees.Update(employee);
            await _context.SaveChangesAsync();
            return employee;
        }

        public async Task DeleteAsync(int id)
        {
            var employee = await GetByIdAsync(id);
            if (employee != null)
            {
                employee.IsActive = false;
                await _context.SaveChangesAsync();
            }
        }
    }
}