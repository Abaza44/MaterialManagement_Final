using MaterialManagement.DAL.Entities;

namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface ISupplierRepo
    {
        Task<IEnumerable<Supplier>> GetAllAsync();
        Task<Supplier?> GetByIdAsync(int id);
        Task<Supplier> AddAsync(Supplier supplier);
        Task<Supplier> UpdateAsync(Supplier supplier);
        Task DeleteAsync(int id);
        Task<IEnumerable<Supplier>> SearchAsync(string searchTerm);
        Task<IEnumerable<Supplier>> GetSuppliersWithBalanceAsync();
        Task<bool> PhoneExistsAsync(string phone, int excludeSupplierId = 0);

        IQueryable<Supplier> GetAsQueryable();
    }
}