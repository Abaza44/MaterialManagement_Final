using MaterialManagement.DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface IMaintenanceRecordRepo
    {
        Task<IEnumerable<MaintenanceRecord>> GetByEquipmentCodeAsync(int equipmentCode);
        Task<MaintenanceRecord> CreateAsync(MaintenanceRecord record);
        Task<MaintenanceRecord?> GetByIdAsync(int id);
        Task UpdateAsync(MaintenanceRecord record);
        Task<bool> DeleteAsync(int id);
    }
}