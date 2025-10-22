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
        public PurchaseInvoiceRepo(MaterialManagementContext context) { _context = context; }

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


        public async Task<PurchaseInvoice> UpdateAsync(PurchaseInvoice invoice)
        {
            _context.PurchaseInvoices.Update(invoice);
            await _context.SaveChangesAsync();
            return invoice;
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
            return _context.PurchaseInvoices.Include(pi => pi.Supplier).Include(pi => pi.Client).AsQueryable();
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

        public void Delete(PurchaseInvoice invoice)
        {
            invoice.IsActive = false; 

        }

        public async Task<PurchaseInvoice?> GetByIdForUpdateAsync(int id)
        {
            return await _context.PurchaseInvoices
                .Include(i => i.PurchaseInvoiceItems) // <<< مهم جدًا لخصم المخزون
                    .ThenInclude(item => item.Material)
                .Include(i => i.Supplier) // <<< مهم جدًا لتعديل رصيد المورد
                .Include(i => i.Client)   // <<< مهم لعكس رصيد العميل (في حالة المرتجع)
                .FirstOrDefaultAsync(i => i.Id == id && i.IsActive);
        }

        public Task AddAsync(PurchaseInvoice invoice)
        {
            _context.PurchaseInvoices.Add(invoice);
            return Task.CompletedTask; // الـ Service هو من سيقوم بالحفظ
        }

        public void Update(PurchaseInvoice invoice)
        {
            _context.PurchaseInvoices.Update(invoice); 
        }

        public async Task<List<PurchaseInvoice>> GetInvoicesForSupplierByDateRangeAsync(int supplierId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.PurchaseInvoices
                .Include(i => i.PurchaseInvoiceItems).ThenInclude(item => item.Material)
                .Where(i => i.SupplierId == supplierId);
            if (fromDate.HasValue) query = query.Where(i => i.InvoiceDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(i => i.InvoiceDate <= toDate.Value);
            return await query.ToListAsync();
        }

        public async Task<List<PurchaseInvoice>> GetReturnsForClientByDateRangeAsync(int clientId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.PurchaseInvoices
                .Include(i => i.PurchaseInvoiceItems).ThenInclude(item => item.Material)
                .Where(i => i.ClientId == clientId); 
            if (fromDate.HasValue) query = query.Where(i => i.InvoiceDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(i => i.InvoiceDate <= toDate.Value);
            return await query.ToListAsync();
        }
    }
}