using MaterialManagement;
using MaterialManagement.DAL.DTOs;
using MaterialManagement.DAL.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface ISalesInvoiceRepo
    {
        Task<IEnumerable<SalesInvoice>> GetAllAsync();
        Task<SalesInvoice?> GetByIdAsync(int id); // للقراءة فقط
        Task<SalesInvoice?> GetByIdForUpdateAsync(int id); // للتحديث والحذف
        Task AddAsync(SalesInvoice invoice); // لا ترجع شيئًا
        Task UpdateAsync(SalesInvoice invoice); // لا ترجع شيئًا
        Task DeleteAsync(int id);
        Task<SalesInvoice?> GetLastInvoiceAsync();

        IQueryable<SalesInvoice> GetAsQueryable();

        Task<IEnumerable<ClientInvoiceSummaryDto>> GetClientInvoiceSummariesAsync();
    }


}