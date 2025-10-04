using MaterialManagement.DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface IClientPaymentRepo
    {
        Task<ClientPayment> CreateAsync(ClientPayment payment);
        Task<IEnumerable<ClientPayment>> GetByClientIdAsync(int clientId);
        Task<IEnumerable<ClientPayment>> GetByInvoiceIdAsync(int invoiceId);
    }
}