using MaterialManagement.BLL.ModelVM.Supplier;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface ISupplierService
    {
        Task<IEnumerable<SupplierViewModel>> GetAllSuppliersAsync();
        Task<SupplierViewModel?> GetSupplierByIdAsync(int id);
        Task<SupplierViewModel> CreateSupplierAsync(SupplierCreateModel model);
        Task<SupplierViewModel> UpdateSupplierAsync(int id, SupplierUpdateModel model);
        Task DeleteSupplierAsync(int id);
        Task<IEnumerable<SupplierViewModel>> SearchSuppliersAsync(string searchTerm);
        Task<IEnumerable<SupplierViewModel>> GetSuppliersWithBalanceAsync();
        Task ReactivateSupplierAsync(int id);
    }
}