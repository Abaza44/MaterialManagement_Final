using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.DAL.Entities;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface IPurchaseInvoiceService
    {
        Task<IEnumerable<PurchaseInvoiceViewModel>> GetAllInvoicesAsync();
        Task<PurchaseInvoiceViewModel?> GetInvoiceByIdAsync(int id);
        Task<PurchaseInvoiceViewModel> CreateInvoiceAsync(PurchaseInvoiceCreateModel model);
        Task DeleteInvoiceAsync(int id);
        Task<IEnumerable<PurchaseInvoiceViewModel>> GetUnpaidInvoicesForSupplierAsync(int supplierId);

        IQueryable<PurchaseInvoice> GetInvoicesAsQueryable();
        Task<IEnumerable<SupplierInvoiceSummaryViewModel>> GetSupplierInvoiceSummariesAsync();


    }
}