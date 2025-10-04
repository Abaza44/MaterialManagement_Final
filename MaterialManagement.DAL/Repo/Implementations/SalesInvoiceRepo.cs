using Microsoft.EntityFrameworkCore;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class SalesInvoiceRepo : ISalesInvoiceRepo
    {
        private readonly MaterialManagementContext _context;

        public SalesInvoiceRepo(MaterialManagementContext context)
        {
            _context = context;
        }


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
                .Include(i => i.Client)
                .Include(i => i.SalesInvoiceItems)
                    .ThenInclude(item => item.Material)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id && i.IsActive);
        }

        public async Task<SalesInvoice?> GetByIdForUpdateAsync(int id)
        {
            // لا نستخدم AsNoTracking لأننا سنقوم بتعديل هذا الكائن
            return await _context.SalesInvoices
                .Include(i => i.SalesInvoiceItems) // مهم لعملية الحذف الآمن
                .FirstOrDefaultAsync(i => i.Id == id && i.IsActive);
        }

        public Task AddAsync(SalesInvoice invoice)
        {
            _context.SalesInvoices.Add(invoice);
            // لا يوجد SaveChangesAsync هنا
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SalesInvoice invoice)
        {
            _context.SalesInvoices.Update(invoice);
            // لا يوجد SaveChangesAsync هنا
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(int id)
        {
            var invoice = await GetByIdForUpdateAsync(id);
            if (invoice != null)
            {
                invoice.IsActive = false; // Soft delete
                // الـ Service هو الذي سيستدعي SaveChangesAsync
            }
        }

        public async Task<SalesInvoice?> GetLastInvoiceAsync()
        {
            return await _context.SalesInvoices
                .OrderByDescending(i => i.Id)
                .FirstOrDefaultAsync();
        }
    }
}