using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.DAL.Entities;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface ISalesInvoiceService
    {
        Task<IEnumerable<SalesInvoiceViewModel>> GetAllInvoicesAsync();
        Task<SalesInvoiceViewModel?> GetInvoiceByIdAsync(int id);
        Task<SalesInvoiceViewModel> CreateInvoiceAsync(SalesInvoiceCreateModel model);
        Task DeleteInvoiceAsync(int id);
        Task<IEnumerable<SalesInvoiceViewModel>> GetUnpaidInvoicesForClientAsync(int clientId);
        IQueryable<SalesInvoice> GetInvoicesAsQueryable();

        Task<IEnumerable<ClientInvoiceSummaryViewModel>> GetClientInvoiceSummariesAsync();
    }
}