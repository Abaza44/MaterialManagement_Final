using MaterialManagement.DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface ISupplierPaymentRepo
    {
        Task<SupplierPayment> CreateAsync(SupplierPayment payment);
        Task<IEnumerable<SupplierPayment>> GetBySupplierIdAsync(int supplierId);
        Task<IEnumerable<SupplierPayment>> GetByInvoiceIdAsync(int invoiceId);
    }
}