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
            ValidateModel(model);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var client = await _clientRepo.GetByIdForUpdateAsync(model.ClientId);
                if (client == null) throw new InvalidOperationException("العميل غير موجود");

                if (model.SalesInvoiceId.HasValue)
                {
                    var invoice = await _invoiceRepo.GetByIdForUpdateAsync(model.SalesInvoiceId.Value);
                    if (invoice == null) throw new InvalidOperationException("الفاتورة غير موجودة");

                    if (invoice.ClientId != model.ClientId)
                        throw new InvalidOperationException("لا يمكن تسجيل تحصيل على فاتورة لا تخص العميل المحدد.");

                    var remainingBeforePayment = CalculateSalesInvoiceRemaining(invoice);
                    if (remainingBeforePayment <= 0)
                        throw new InvalidOperationException("لا يوجد مبلغ متبقٍ على هذه الفاتورة.");

                    if (model.Amount > remainingBeforePayment)
                        throw new InvalidOperationException($"لا يمكن تسجيل تحصيل أكبر من المتبقي على الفاتورة. المتبقي: {remainingBeforePayment:N2}.");

                    invoice.PaidAmount += model.Amount;
                    invoice.RemainingAmount = CalculateSalesInvoiceRemaining(invoice);
                }
                else
                {
                    if (client.Balance <= 0)
                        throw new InvalidOperationException("لا يوجد رصيد مستحق على العميل لتسجيل تحصيل غير مرتبط بفاتورة.");

                    if (model.Amount > client.Balance)
                        throw new InvalidOperationException($"لا يمكن تسجيل تحصيل غير مرتبط بفاتورة أكبر من رصيد العميل المستحق. الرصيد الحالي: {client.Balance:N2}.");
                }

                client.Balance -= model.Amount;

                var payment = _mapper.Map<ClientPayment>(model);
                payment.PaymentDate = DateTime.Now;
                await _clientPaymentRepo.CreateAsync(payment);

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

        private static void ValidateModel(ClientPaymentCreateModel model)
        {
            if (model == null)
                throw new InvalidOperationException("بيانات التحصيل مطلوبة.");

            if (model.ClientId <= 0)
                throw new InvalidOperationException("يجب اختيار العميل.");

            if (model.SalesInvoiceId.HasValue && model.SalesInvoiceId.Value <= 0)
                throw new InvalidOperationException("الفاتورة المحددة غير صالحة.");

            if (model.Amount <= 0)
                throw new InvalidOperationException("المبلغ يجب أن يكون أكبر من الصفر.");
        }

        private static decimal CalculateSalesInvoiceRemaining(SalesInvoice invoice)
        {
            return (invoice.TotalAmount - invoice.DiscountAmount) - invoice.PaidAmount;
        }

        public async Task<IEnumerable<ClientPaymentViewModel>> GetPaymentsForClientAsync(int clientId)
        {
            
            var payments = await _clientPaymentRepo.GetByClientIdAsync(clientId);

            
            return _mapper.Map<IEnumerable<ClientPaymentViewModel>>(payments);
        }
    }
}
