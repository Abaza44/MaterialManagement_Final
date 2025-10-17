using MaterialManagement.BLL.ModelVM.Equipment;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface IEquipmentService
    {
        Task<IEnumerable<EquipmentViewModel>> GetAllEquipmentAsync();
        Task<EquipmentViewModel?> GetByCodeAsync(int code);
        Task<EquipmentViewModel> CreateEquipmentAsync(EquipmentCreateModel model);
        Task<EquipmentViewModel> UpdateEquipmentAsync(EquipmentUpdateModel model);
        Task<bool> DeleteEquipmentAsync(int code);
        Task<EquipmentUpdateModel?> GetEquipmentForUpdateAsync(int code);

        IQueryable<EquipmentViewModel> GetEquipmentAsQueryable();
    }
}