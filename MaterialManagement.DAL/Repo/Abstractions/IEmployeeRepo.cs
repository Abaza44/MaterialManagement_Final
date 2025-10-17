using MaterialManagement.DAL.Entities;

namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface IEmployeeRepo
    {
        Task<IEnumerable<Employee>> GetAllAsync();
        Task<Employee?> GetByIdAsync(int id);
        Task<Employee> CreateAsync(Employee employee);
        Task<Employee> UpdateAsync(Employee employee);
        Task DeleteAsync(int id);
        IQueryable<Employee> GetAsQueryable();
    }
}