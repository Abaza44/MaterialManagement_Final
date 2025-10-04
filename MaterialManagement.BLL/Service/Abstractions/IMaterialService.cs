using MaterialManagement.BLL.ModelVM.Material;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface IMaterialService
    {
        Task<IEnumerable<MaterialViewModel>> GetAllMaterialsAsync();
        Task<MaterialViewModel?> GetMaterialByIdAsync(int id);
        Task<MaterialViewModel> CreateMaterialAsync(MaterialCreateModel model);
        Task<MaterialViewModel> UpdateMaterialAsync(int id, MaterialUpdateModel model);
        Task DeleteMaterialAsync(int id);
        Task<IEnumerable<MaterialViewModel>> SearchMaterialsAsync(string searchTerm);
        Task<IEnumerable<MaterialViewModel>> GetLowStockMaterialsAsync();
        Task UpdateMaterialQuantityAsync(int materialId, decimal quantity, bool isAddition);
        Task<bool> IsCodeExistsAsync(string code, int? excludeId = null);
    }
}