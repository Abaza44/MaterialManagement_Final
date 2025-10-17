using Microsoft.EntityFrameworkCore;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class MaterialRepo : IMaterialRepo
    {
        private readonly MaterialManagementContext _context;

        public MaterialRepo(MaterialManagementContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Material>> GetAllAsync()
        {
            return await _context.Materials
                .Where(m => m.IsActive)
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<Material?> GetByIdAsync(int id)
        {
            return await _context.Materials
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
        }

        public async Task<Material> AddAsync(Material material)
        {
            await _context.Materials.AddAsync(material);
            await _context.SaveChangesAsync();
            return material;
        }

        public async Task<Material> UpdateAsync(Material material)
        {
            _context.Materials.Update(material);
            await _context.SaveChangesAsync();
            return material;
        }

        public async Task DeleteAsync(int id)
        {
            var material = await GetByIdAsync(id);
            if (material != null)
            {
                material.IsActive = false;
                await UpdateAsync(material);
            }
        }

        public async Task<IEnumerable<Material>> SearchAsync(string searchTerm)
        {
            return await _context.Materials
                .Where(m => m.IsActive &&
                           (m.Name.Contains(searchTerm) ||
                            m.Code.Contains(searchTerm) ||
                            (m.Description != null && m.Description.Contains(searchTerm))))
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Material>> GetLowStockMaterialsAsync(decimal threshold = 10)
        {
            return await _context.Materials
                .Where(m => m.IsActive && m.Quantity <= threshold)
                .OrderBy(m => m.Quantity)
                .ToListAsync();
        }

        public async Task<Material?> GetByCodeAsync(string code)
        {
            return await _context.Materials
                .FirstOrDefaultAsync(m => m.Code == code && m.IsActive);
        }
        public async Task<Material?> GetByIdForUpdateAsync(int id)
        {
            
            return await _context.Materials.FindAsync(id);
        }

        public IQueryable<Material> GetAsQueryable()
        {
            
            return _context.Materials.AsQueryable();
        }
    }
}