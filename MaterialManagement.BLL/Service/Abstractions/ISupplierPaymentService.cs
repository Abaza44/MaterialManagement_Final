using MaterialManagement.BLL.ModelVM.Payment;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface ISupplierPaymentService
    {
        Task<SupplierPaymentViewModel> AddPaymentAsync(SupplierPaymentCreateModel model);
        Task<IEnumerable<SupplierPaymentViewModel>> GetPaymentsForSupplierAsync(int supplierId);
        Task<IEnumerable<SupplierPaymentViewModel>> GetPaymentsForInvoiceAsync(int invoiceId);
    }
}