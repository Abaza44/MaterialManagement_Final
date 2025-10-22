using MaterialManagement.DAL.DTOs;
using MaterialManagement.DAL.Entities;
namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface IPurchaseInvoiceRepo
    {
        Task<IEnumerable<PurchaseInvoice>> GetAllAsync();
        Task<PurchaseInvoice?> GetByIdAsync(int id);
        Task<PurchaseInvoice?> GetByInvoiceNumberAsync(string invoiceNumber);
        Task AddAsync(PurchaseInvoice invoice);
        void Update(PurchaseInvoice invoice); 
        void Delete(PurchaseInvoice invoice); 
        Task<IEnumerable<PurchaseInvoice>> GetBySupplierIdAsync(int supplierId);
        Task<PurchaseInvoice?> GetLastInvoiceAsync();
        IQueryable<PurchaseInvoice> GetAsQueryable();
        Task<IEnumerable<SupplierInvoicesDto>> GetSupplierInvoiceSummariesAsync();
        Task<PurchaseInvoice?> GetByIdForUpdateAsync(int id);
        Task<List<PurchaseInvoice>> GetInvoicesForSupplierByDateRangeAsync(int supplierId, DateTime? fromDate, DateTime? toDate);
        Task<List<PurchaseInvoice>> GetReturnsForClientByDateRangeAsync(int clientId, DateTime? fromDate, DateTime? toDate);
    }
}