using MaterialManagement.BLL.ModelVM.Maintenance;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface IMaintenanceService
    {
        Task<IEnumerable<MaintenanceRecordViewModel>> GetHistoryForEquipmentAsync(int equipmentCode);
        Task<MaintenanceRecordViewModel> AddMaintenanceRecordAsync(MaintenanceRecordCreateModel model);
    }
}