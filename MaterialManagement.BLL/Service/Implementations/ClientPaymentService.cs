using AutoMapper;
using MaterialManagement.BLL.ModelVM.Payment;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using MaterialManagement.DAL.Repo.Implementations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class ClientPaymentService : IClientPaymentService
    {
        
        private readonly MaterialManagementContext _context; 
        private readonly IMapper _mapper;
        private readonly IClientPaymentRepo _clientPaymentRepo;

        public ClientPaymentService(IClientPaymentRepo paymentRepo, MaterialManagementContext context, IMapper mapper, IClientPaymentRepo clientPaymentRepo)
        {

            _context = context;
            _mapper = mapper;
            _clientPaymentRepo = clientPaymentRepo;
        }

        public async Task<ClientPaymentViewModel> AddPaymentAsync(ClientPaymentCreateModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. إنشاء كيان الدفع (في الذاكرة فقط)
                var payment = _mapper.Map<ClientPayment>(model);
                payment.PaymentDate = DateTime.Now;

                // <<< الإصلاح الحاسم (1): الإضافة مباشرة إلى الـ context >>>
                _context.ClientPayments.Add(payment);

                // 2. جلب العميل للتتبع والتحديث
                // <<< الإصلاح الحاسم (2): استخدام _context مباشرة >>>
                var client = await _context.Clients.FindAsync(model.ClientId);
                if (client == null) throw new InvalidOperationException("Client not found");

                client.Balance -= model.Amount;


                if (model.SalesInvoiceId.HasValue)
                {
                    var invoice = await _context.SalesInvoices.FindAsync(model.SalesInvoiceId.Value);
                    if (invoice == null) throw new InvalidOperationException("Invoice not found");

                    invoice.PaidAmount += model.Amount;
                    invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
                }

                // 4. حفظ كل التغييرات التي يتتبعها _context
                await _context.SaveChangesAsync();

                // 5. تأكيد الـ Transaction
                await transaction.CommitAsync();

                var viewModel = _mapper.Map<ClientPaymentViewModel>(payment);
                viewModel.ClientName = client.Name;
                return viewModel;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<IEnumerable<ClientPaymentViewModel>> GetPaymentsForClientAsync(int clientId)
        {
            
            var payments = await _clientPaymentRepo.GetByClientIdAsync(clientId);

            
            return _mapper.Map<IEnumerable<ClientPaymentViewModel>>(payments);
        }
    }
}