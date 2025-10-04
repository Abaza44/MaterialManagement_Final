using MaterialManagement.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface IMaterialRepo
    {
        Task<IEnumerable<Material>> GetAllAsync();
        Task<Material?> GetByIdAsync(int id);
        Task<Material> AddAsync(Material material);
        Task<Material> UpdateAsync(Material material);
        Task DeleteAsync(int id);
        Task<IEnumerable<Material>> SearchAsync(string searchTerm);
        Task<IEnumerable<Material>> GetLowStockMaterialsAsync(decimal threshold = 10);
        Task<Material?> GetByCodeAsync(string code);

        Task<Material?> GetByIdForUpdateAsync(int id);
    }
}
