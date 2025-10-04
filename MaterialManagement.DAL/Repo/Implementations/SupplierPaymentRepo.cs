using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class SupplierPaymentRepo : ISupplierPaymentRepo
    {
        private readonly MaterialManagementContext _context;
        public SupplierPaymentRepo(MaterialManagementContext context) { _context = context; }

        public async Task<SupplierPayment> CreateAsync(SupplierPayment payment)
        {
            _context.SupplierPayments.Add(payment);
            // الحفظ سيتم في الـ Service داخل Transaction
            return payment;
        }

        public async Task<IEnumerable<SupplierPayment>> GetBySupplierIdAsync(int supplierId)
        {
            return await _context.SupplierPayments
                .Where(p => p.SupplierId == supplierId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<SupplierPayment>> GetByInvoiceIdAsync(int invoiceId)
        {
            return await _context.SupplierPayments
                .Where(p => p.PurchaseInvoiceId == invoiceId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }
    }
}