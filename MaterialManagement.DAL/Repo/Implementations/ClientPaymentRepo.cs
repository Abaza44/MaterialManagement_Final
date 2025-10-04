using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class ClientPaymentRepo : IClientPaymentRepo
    {
        private readonly MaterialManagementContext _context;
        public ClientPaymentRepo(MaterialManagementContext context) { _context = context; }

        public async Task<ClientPayment> CreateAsync(ClientPayment payment)
        {
            _context.ClientPayments.Add(payment);
            // الحفظ سيتم في الـ Service
            return payment;
        }

        public async Task<IEnumerable<ClientPayment>> GetByClientIdAsync(int clientId)
        {
            return await _context.ClientPayments
                .Where(p => p.ClientId == clientId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<ClientPayment>> GetByInvoiceIdAsync(int invoiceId)
        {
            return await _context.ClientPayments
                .Where(p => p.SalesInvoiceId == invoiceId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }
    }
}