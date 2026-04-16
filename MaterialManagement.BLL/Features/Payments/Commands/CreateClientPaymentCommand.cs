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
        public ClientPaymentCreateModel Model { get; set; } = new();
    }

    // 2. The Validator
    public class CreateClientPaymentValidator : AbstractValidator<CreateClientPaymentCommand>
    {
        public CreateClientPaymentValidator()
        {
            RuleFor(x => x.Model)
                .NotNull().WithMessage("بيانات التحصيل مطلوبة.");

            When(x => x.Model != null, () =>
            {
                RuleFor(x => x.Model.ClientId)
                    .GreaterThan(0).WithMessage("يجب اختيار العميل.");

                RuleFor(x => x.Model.SalesInvoiceId)
                    .GreaterThan(0)
                    .When(x => x.Model.SalesInvoiceId.HasValue)
                    .WithMessage("الفاتورة المحددة غير صالحة.");

                RuleFor(x => x.Model.Amount)
                    .GreaterThan(0).WithMessage("المبلغ يجب أن يكون أكبر من الصفر.");
            });
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
            var validator = new CreateClientPaymentValidator();
            await validator.ValidateAndThrowAsync(request, cancellationToken);

            // Transaction started to ensure exact parity with legacy service
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var model = request.Model;

                // 2. Update Client (Using optimistic concurrency instead of GetByIdForUpdateAsync)
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == model.ClientId, cancellationToken);
                if (client == null) throw new InvalidOperationException("العميل غير موجود");

                // 3. Update Invoice if assigned
                if (model.SalesInvoiceId.HasValue)
                {
                    var invoice = await _context.SalesInvoices.FirstOrDefaultAsync(i => i.Id == model.SalesInvoiceId.Value, cancellationToken);
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

                // 1. Create Payment
                var payment = _mapper.Map<ClientPayment>(model);
                payment.PaymentDate = DateTime.Now;
                await _context.ClientPayments.AddAsync(payment, cancellationToken);

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

        private static decimal CalculateSalesInvoiceRemaining(SalesInvoice invoice)
        {
            return (invoice.TotalAmount - invoice.DiscountAmount) - invoice.PaidAmount;
        }
    }
}
