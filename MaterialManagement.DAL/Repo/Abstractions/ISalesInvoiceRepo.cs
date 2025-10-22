// ISalesInvoiceRepo.cs
using MaterialManagement.DAL.DTOs;
using MaterialManagement.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface ISalesInvoiceRepo
    {
        Task<IEnumerable<SalesInvoice>> GetAllAsync();
        Task<SalesInvoice?> GetByIdAsync(int id); // للقراءة فقط (AsNoTracking)
        Task<SalesInvoice?> GetByIdWithDetailsAsync(int id); // للقراءة التفصيلية (AsNoTracking)
        Task<SalesInvoice?> GetByIdForUpdateAsync(int id); // للتحديث والحذف (Tracking)
        Task AddAsync(SalesInvoice invoice);
        void Update(SalesInvoice invoice);
        void Delete(SalesInvoice invoice); // <<< تم التعديل هنا (لا تحفظ)

        // دوال مساعدة
        IQueryable<SalesInvoice> GetAsQueryable();
        Task<IEnumerable<ClientInvoiceSummaryDto>> GetClientInvoiceSummariesAsync();
        Task<List<SalesInvoice>> GetInvoicesForClientByDateRangeAsync(int clientId, DateTime? fromDate, DateTime? toDate);
    }
}