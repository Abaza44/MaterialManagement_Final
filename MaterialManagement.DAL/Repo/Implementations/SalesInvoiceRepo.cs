using Microsoft.EntityFrameworkCore;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaterialManagement.DAL.DTOs;
using MaterialManagement.DAL.Enums;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class SalesInvoiceRepo : ISalesInvoiceRepo
    {
        private readonly MaterialManagementContext _context;
        public SalesInvoiceRepo(MaterialManagementContext context) { _context = context; }

        public async Task<IEnumerable<SalesInvoice>> GetAllAsync()
        {
            return await _context.SalesInvoices
                .Include(si => si.Client)
                .Where(si => si.IsActive)
                .OrderByDescending(si => si.InvoiceDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<SalesInvoice?> GetByIdAsync(int id)
        {
            return await _context.SalesInvoices
                .IgnoreQueryFilters()
                .Include(i => i.Client)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id && i.IsActive);
        }

        // دالة لجلب كل التفاصيل للقراءة (مثل صفحة التفاصيل)
        public async Task<SalesInvoice?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.SalesInvoices
               .IgnoreQueryFilters()
               .Include(i => i.Client)
               .Include(i => i.SalesInvoiceItems)
                   .ThenInclude(item => item.Material)
               .AsNoTracking()
               .FirstOrDefaultAsync(i => i.Id == id && i.IsActive);
        }

        // دالة لجلب الفاتورة للتعديل أو الحذف (تتبع التغييرات)
        public async Task<SalesInvoice?> GetByIdForUpdateAsync(int id)
        {
            return await _context.SalesInvoices
                .IgnoreQueryFilters()
                .Include(i => i.SalesInvoiceItems) // <<< مهم جدًا لإرجاع المخزون
                    .ThenInclude(item => item.Material)
                .Include(i => i.Client) // <<< مهم جدًا لتعديل رصيد العميل
                .FirstOrDefaultAsync(i => i.Id == id && i.IsActive);
        }

        public Task AddAsync(SalesInvoice invoice)
        {
            _context.SalesInvoices.Add(invoice);
            return Task.CompletedTask; // الـ Service هو من سيقوم بالحفظ
        }

        public void Update(SalesInvoice invoice)
        {
            _context.SalesInvoices.Update(invoice); // الـ Service هو من سيقوم بالحفظ
        }

        // الحذف الناعم (Soft Delete) - هذه الدالة لا تحفظ
        public void Delete(SalesInvoice invoice)
        {
            invoice.IsActive = false;
            // لا يوجد حفظ هنا، الـ Service هو المسؤول
        }

        public IQueryable<SalesInvoice> GetAsQueryable()
        {
            return _context.SalesInvoices.Include(si => si.Client).AsQueryable();
        }

        public async Task<IEnumerable<ClientInvoiceSummaryDto>> GetClientInvoiceSummariesAsync()
        {
            var registeredSummaries = await _context.SalesInvoices
               .Where(si => si.IsActive && si.PartyMode == SalesInvoicePartyMode.RegisteredClient && si.Client != null)
               .GroupBy(si => si.Client!)
               .Select(group => new ClientInvoiceSummaryDto
               {
                   ClientId = group.Key.Id,
                   ClientName = group.Key.Name,
                   InvoiceCount = group.Count(),
                   TotalDebt = group.Key.Balance
               })
               .OrderBy(summary => summary.ClientName)
               .AsNoTracking()
               .ToListAsync();

            var walkInSummary = await _context.SalesInvoices
                .Where(si => si.IsActive && si.PartyMode == SalesInvoicePartyMode.WalkInCustomer)
                .GroupBy(si => 1)
                .Select(group => new ClientInvoiceSummaryDto
                {
                    ClientId = 0,
                    ClientName = "عملاء نقديون / بدون تسجيل",
                    InvoiceCount = group.Count(),
                    TotalDebt = group.Sum(si => si.RemainingAmount)
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (walkInSummary != null)
            {
                registeredSummaries.Add(walkInSummary);
            }

            return registeredSummaries;
        }

        public async Task<List<SalesInvoice>> GetInvoicesForClientByDateRangeAsync(int clientId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.SalesInvoices
               .IgnoreQueryFilters()
               .Include(i => i.SalesInvoiceItems)
                   .ThenInclude(item => item.Material)
               .Where(i => i.ClientId == clientId && i.IsActive);

            if (fromDate.HasValue) query = query.Where(i => i.InvoiceDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(i => i.InvoiceDate <= toDate.Value);

            return await query.ToListAsync();
        }
    }
}
