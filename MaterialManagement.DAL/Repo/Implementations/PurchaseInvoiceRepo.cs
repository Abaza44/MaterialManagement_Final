using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.DTOs;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class PurchaseInvoiceRepo : IPurchaseInvoiceRepo
    {
        private readonly MaterialManagementContext _context;

        public PurchaseInvoiceRepo(MaterialManagementContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<PurchaseInvoice>> GetAllAsync()
        {
            return await _context.PurchaseInvoices
                .Include(pi => pi.Supplier)
                .Include(pi => pi.PurchaseInvoiceItems)
                    .ThenInclude(item => item.Material)
                .Where(pi => pi.IsActive)
                .OrderByDescending(pi => pi.InvoiceDate)
                .ToListAsync();
        }

        public async Task<PurchaseInvoice?> GetByIdAsync(int id)
        {
            return await _context.PurchaseInvoices
                .Include(i => i.Supplier) // لجلب اسم المورد
                .Include(i => i.PurchaseInvoiceItems) // لجلب بنود الفاتورة
                    .ThenInclude(item => item.Material) // ولكل بند، اجلب تفاصيل المادة
                .AsNoTracking() // للقراءة فقط، أداء أفضل
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<PurchaseInvoice?> GetByInvoiceNumberAsync(string invoiceNumber)
        {
            return await _context.PurchaseInvoices
                .Include(pi => pi.Supplier)
                .Include(pi => pi.PurchaseInvoiceItems)
                    .ThenInclude(item => item.Material)
                .FirstOrDefaultAsync(pi => pi.InvoiceNumber == invoiceNumber && pi.IsActive);
        }

        public async Task<PurchaseInvoice> AddAsync(PurchaseInvoice invoice)
        {
            await _context.PurchaseInvoices.AddAsync(invoice);
            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task<PurchaseInvoice> UpdateAsync(PurchaseInvoice invoice)
        {
            _context.PurchaseInvoices.Update(invoice);
            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task DeleteAsync(int id)
        {
            var invoice = await GetByIdAsync(id);
            if (invoice != null)
            {
                invoice.IsActive = false; // soft delete
                await UpdateAsync(invoice);
            }
        }

        public async Task<IEnumerable<PurchaseInvoice>> GetBySupplierIdAsync(int supplierId)
        {
            return await _context.PurchaseInvoices
                .Where(pi => pi.SupplierId == supplierId && pi.IsActive)
                .Include(pi => pi.PurchaseInvoiceItems)
                .ToListAsync();
        }

        public async Task<PurchaseInvoice?> GetLastInvoiceAsync()
        {
            return await _context.PurchaseInvoices
                .OrderByDescending(i => i.Id)
                .FirstOrDefaultAsync();
        }
        public IQueryable<PurchaseInvoice> GetAsQueryable()
        {

            return _context.PurchaseInvoices
                .Include(pi => pi.Supplier)
                .Include(pi => pi.Client)
                .AsQueryable();
        }

        public async Task<IEnumerable<SupplierInvoicesDto>> GetSupplierInvoiceSummariesAsync()
        {
            return await _context.PurchaseInvoices
                .Where(pi => pi.IsActive && pi.SupplierId != null)
                .GroupBy(pi => pi.Supplier)
                .Select(group => new SupplierInvoicesDto
                {
                    SupplierId = group.Key.Id,
                    SupplierName = group.Key.Name,
                    InvoiceCount = group.Count(),
                    TotalCredit = group.Key.Balance
                })
                .OrderBy(summary => summary.SupplierName)
                .AsNoTracking()
                .ToListAsync();
        }

    }
}