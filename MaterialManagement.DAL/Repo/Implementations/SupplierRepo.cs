using Microsoft.EntityFrameworkCore;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class SupplierRepo : ISupplierRepo
    {
        private readonly MaterialManagementContext _context;

        public SupplierRepo(MaterialManagementContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Supplier>> GetAllAsync()
        {
            return await _context.Suppliers
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<Supplier?> GetByIdAsync(int id)
        {
            return await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Supplier> AddAsync(Supplier supplier)
        {
            await _context.Suppliers.AddAsync(supplier);
            await _context.SaveChangesAsync();
            return supplier;
        }

        public async Task<Supplier> UpdateAsync(Supplier supplier)
        {
            _context.Suppliers.Update(supplier);
            await _context.SaveChangesAsync();
            return supplier;
        }

        public async Task DeleteAsync(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Supplier>> SearchAsync(string searchTerm)
        {
            return await _context.Suppliers
                .Where(s => s.IsActive &&
                           (s.Name.Contains(searchTerm) ||
                            (s.Phone != null && s.Phone.Contains(searchTerm)) ||
                            (s.Address != null && s.Address.Contains(searchTerm))))
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Supplier>> GetSuppliersWithBalanceAsync()
        {
            return await _context.Suppliers
                .Where(s => s.IsActive && s.Balance != 0)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }
        public async Task ReactivateAsync(int id)
        {
            var supplier = await GetByIdAsync(id);
            if (supplier != null && !supplier.IsActive)
            {
                supplier.IsActive = true;
                await UpdateAsync(supplier);
            }
        }
        public async Task<bool> PhoneExistsAsync(string phone, int excludeSupplierId = 0)
        {
            return await _context.Suppliers.AnyAsync(s => s.Phone == phone && s.Id != excludeSupplierId);
        }
        public IQueryable<Supplier> GetAsQueryable()
        {
            return _context.Suppliers.AsQueryable();
        }
        public async Task<Supplier?> GetByIdForUpdateAsync(int id)
        {
            return await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
        }
    }
}