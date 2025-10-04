using MaterialManagement.DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface IEquipmentRepo
    {
        Task<IEnumerable<Equipment>> GetAllAsync();
        Task<Equipment?> GetByCodeAsync(int code);
        Task<Equipment> CreateAsync(Equipment equipment);
        Task UpdateAsync(Equipment equipment);
        Task<bool> DeleteAsync(int code);
    }
}