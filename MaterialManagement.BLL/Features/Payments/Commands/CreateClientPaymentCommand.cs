using AutoMapper;
using FluentValidation;
using MaterialManagement.BLL.ModelVM.Payment;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Features.Payments.Commands
{
    // 1. The Command
    public class CreateClientPaymentCommand : IRequest<ClientPaymentViewModel>
    {
        public ClientPaymentCreateModel Model { get; set; }
    }

    // 2. The Validator
    public class CreateClientPaymentValidator : AbstractValidator<CreateClientPaymentCommand>
    {
        public CreateClientPaymentValidator()
        {
            RuleFor(x => x.Model.ClientId)
                .GreaterThan(0).WithMessage("يجب اختيار العميل.");
                
            RuleFor(x => x.Model.Amount)
                .GreaterThan(0).WithMessage("المبلغ يجب أن يكون أكبر من الصفر.");
        }
    }

    // 3. The Handler (Direct DbContext Integration)
    public class CreateClientPaymentHandler : IRequestHandler<CreateClientPaymentCommand, ClientPaymentViewModel>
    {
        private readonly MaterialManagementContext _context;
        private readonly IMapper _mapper;

        public CreateClientPaymentHandler(MaterialManagementContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<ClientPaymentViewModel> Handle(CreateClientPaymentCommand request, CancellationToken cancellationToken)
        {
            // Transaction started to ensure exact parity with legacy service
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var model = request.Model;

                // 1. Create Payment
                var payment = _mapper.Map<ClientPayment>(model);
                payment.PaymentDate = DateTime.Now;
                await _context.ClientPayments.AddAsync(payment, cancellationToken);

                // 2. Update Client (Using optimistic concurrency instead of GetByIdForUpdateAsync)
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == model.ClientId, cancellationToken);
                if (client == null) throw new InvalidOperationException("العميل غير موجود");

                client.Balance -= model.Amount;

                // 3. Update Invoice if assigned
                if (model.SalesInvoiceId.HasValue)
                {
                    var invoice = await _context.SalesInvoices.FirstOrDefaultAsync(i => i.Id == model.SalesInvoiceId.Value, cancellationToken);
                    if (invoice == null) throw new InvalidOperationException("الفاتورة غير موجودة");

                    invoice.PaidAmount += model.Amount;
                    invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
                }

                // 4. Save to DB. This is where DbUpdateConcurrencyException will be thrown if RowVersion mismatches!
                await _context.SaveChangesAsync(cancellationToken);
                
                // 5. Commit Transaction
                await transaction.CommitAsync(cancellationToken);

                var viewModel = _mapper.Map<ClientPaymentViewModel>(payment);
                viewModel.ClientName = client.Name;
                return viewModel;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
