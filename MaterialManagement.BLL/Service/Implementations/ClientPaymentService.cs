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

        private readonly IClientPaymentRepo _clientPaymentRepo;
        private readonly IClientRepo _clientRepo;
        private readonly ISalesInvoiceRepo _invoiceRepo;
        private readonly MaterialManagementContext _context;
        private readonly IMapper _mapper;


        // --- 2. UPDATE THE CONSTRUCTOR TO ASSIGN IT ---
        public ClientPaymentService(
        IClientPaymentRepo clientPaymentRepo,
        IClientRepo clientRepo,
        ISalesInvoiceRepo invoiceRepo,
        MaterialManagementContext context,
        IMapper mapper)
        {
           
            _clientPaymentRepo = clientPaymentRepo;
            _clientRepo = clientRepo;
            _invoiceRepo = invoiceRepo;
            _context = context;
            _mapper = mapper;
        }

        public async Task<ClientPaymentViewModel> AddPaymentAsync(ClientPaymentCreateModel model)
        {

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {

                var payment = _mapper.Map<ClientPayment>(model);
                payment.PaymentDate = DateTime.Now;
                await _clientPaymentRepo.CreateAsync(payment);


                var client = await _clientRepo.GetByIdForUpdateAsync(model.ClientId);
                if (client == null) throw new InvalidOperationException("العميل غير موجود");

                client.Balance -= model.Amount;


                if (model.SalesInvoiceId.HasValue)
                {
                    var invoice = await _invoiceRepo.GetByIdForUpdateAsync(model.SalesInvoiceId.Value);
                    if (invoice == null) throw new InvalidOperationException("الفاتورة غير موجودة");

                    invoice.PaidAmount += model.Amount;
                    invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
                }

                await _context.SaveChangesAsync();
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