using MaterialManagement.BLL.ModelVM.Payment;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface IClientPaymentService
    {
        Task<ClientPaymentViewModel> AddPaymentAsync(ClientPaymentCreateModel model);
    }
}