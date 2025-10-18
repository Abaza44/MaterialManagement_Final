using MaterialManagement.DAL.DTOs;
using MaterialManagement.DAL.Entities;
namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface IPurchaseInvoiceRepo
    {
        Task<IEnumerable<PurchaseInvoice>> GetAllAsync();
        Task<PurchaseInvoice?> GetByIdAsync(int id);
        Task<PurchaseInvoice?> GetByInvoiceNumberAsync(string invoiceNumber);
        Task<PurchaseInvoice> AddAsync(PurchaseInvoice invoice);
        Task<PurchaseInvoice> UpdateAsync(PurchaseInvoice invoice);
        Task DeleteAsync(int id);
        Task<IEnumerable<PurchaseInvoice>> GetBySupplierIdAsync(int supplierId);

        Task<PurchaseInvoice?> GetLastInvoiceAsync();
        IQueryable<PurchaseInvoice> GetAsQueryable();
        Task<IEnumerable<SupplierInvoicesDto>> GetSupplierInvoiceSummariesAsync();

    }
}