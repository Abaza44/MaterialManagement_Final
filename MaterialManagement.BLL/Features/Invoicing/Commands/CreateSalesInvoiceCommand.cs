using AutoMapper;
using FluentValidation;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Features.Invoicing.Commands
{
    // 1. The Command
    public class CreateSalesInvoiceCommand : IRequest<SalesInvoiceViewModel>
    {
        public SalesInvoiceCreateModel Model { get; set; } = new();
    }

    // 2. The Validator
    public class CreateSalesInvoiceValidator : AbstractValidator<CreateSalesInvoiceCommand>
    {
        public CreateSalesInvoiceValidator()
        {
            RuleFor(x => x.Model)
                .NotNull().WithMessage("بيانات فاتورة البيع مطلوبة.");

            When(x => x.Model != null, () =>
            {
                When(x => x.Model.PartyMode == SalesInvoicePartyMode.RegisteredClient, () =>
                {
                    RuleFor(x => x.Model.ClientId)
                        .NotNull().WithMessage("يجب اختيار العميل.")
                        .GreaterThan(0).WithMessage("يجب اختيار العميل.");
                });

                When(x => x.Model.PartyMode == SalesInvoicePartyMode.WalkInCustomer, () =>
                {
                    RuleFor(x => x.Model.OneTimeCustomerName)
                        .NotEmpty().WithMessage("يجب إدخال اسم العميل النقدي.");
                });

                RuleFor(x => x.Model.DiscountAmount)
                    .GreaterThanOrEqualTo(0).WithMessage("مبلغ الخصم لا يمكن أن يكون سالباً.");

                RuleFor(x => x.Model.PaidAmount)
                    .GreaterThanOrEqualTo(0).WithMessage("المبلغ المدفوع لا يمكن أن يكون سالباً.");

                RuleFor(x => x.Model.Items)
                    .NotEmpty().WithMessage("يجب إضافة بند واحد على الأقل للفاتورة.");

                RuleForEach(x => x.Model.Items).ChildRules(items => {
                    items.RuleFor(i => i.MaterialId)
                         .GreaterThan(0).WithMessage("يجب اختيار مادة صحيحة لكل بند.");
                    items.RuleFor(i => i.Quantity)
                         .GreaterThan(0).WithMessage("الكمية يجب أن تكون أكبر من الصفر.");
                    items.RuleFor(i => i.UnitPrice)
                         .GreaterThan(0).WithMessage("سعر الوحدة يجب أن يكون أكبر من الصفر.");
                });
            });
        }
    }

    // 3. The Handler
    public class CreateSalesInvoiceHandler : IRequestHandler<CreateSalesInvoiceCommand, SalesInvoiceViewModel>
    {
        private readonly MaterialManagementContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<CreateSalesInvoiceHandler> _logger;

        public CreateSalesInvoiceHandler(MaterialManagementContext context, IMapper mapper, ILogger<CreateSalesInvoiceHandler> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<SalesInvoiceViewModel> Handle(CreateSalesInvoiceCommand request, CancellationToken cancellationToken)
        {
            var validator = new CreateSalesInvoiceValidator();
            await validator.ValidateAndThrowAsync(request, cancellationToken);

            var transactionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _logger.LogInformation("[SalesInvoice:MediatR] Transaction {TxId} STARTED.", transactionId);

            // 1. STRICT ISOLATED TRANSACTION
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var model = request.Model;
                
                var partyMode = ResolvePartyMode(model);
                Client? client = null;

                // Fetch Client with EF Tracking
                if (partyMode == SalesInvoicePartyMode.RegisteredClient)
                {
                    client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == model.ClientId!.Value, cancellationToken);
                    if (client == null) throw new InvalidOperationException("العميل غير موجود");
                }

                var invoice = new SalesInvoice
                {
                    PartyMode = partyMode,
                    ClientId = partyMode == SalesInvoicePartyMode.RegisteredClient ? model.ClientId : null,
                    OneTimeCustomerName = partyMode == SalesInvoicePartyMode.WalkInCustomer ? model.OneTimeCustomerName?.Trim() : null,
                    OneTimeCustomerPhone = partyMode == SalesInvoicePartyMode.WalkInCustomer ? NormalizeOptionalText(model.OneTimeCustomerPhone) : null,
                    InvoiceDate = DateTime.Now,
                    InvoiceNumber = $"SAL-{DateTime.Now.Ticks}",
                    Notes = NormalizeOptionalText(model.Notes)
                };

                decimal totalAmount = 0;
                foreach (var item in model.Items)
                {
                    // Fetch Material with pure EF Core Tracking limits
                    var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == item.MaterialId, cancellationToken);
                    if (material == null) throw new InvalidOperationException($"المادة غير موجودة");
                    if (material.Quantity < item.Quantity) throw new InvalidOperationException($"الكمية غير كافية للمادة: '{material.Name}'.");

                    // Immediate Stock Subtraction
                    material.Quantity -= item.Quantity;

                    var itemTotal = item.Quantity * item.UnitPrice;
                    totalAmount += itemTotal;

                    var invoiceItem = _mapper.Map<SalesInvoiceItem>(item);
                    invoiceItem.TotalPrice = itemTotal;
                    invoice.SalesInvoiceItems.Add(invoiceItem);
                }

                // Core Mathematics Identical to Legacy
                invoice.TotalAmount = totalAmount;
                invoice.PaidAmount = model.PaidAmount;
                invoice.DiscountAmount = model.DiscountAmount;
                
                decimal netAmountDue = invoice.TotalAmount - invoice.DiscountAmount;
                ValidateTotals(invoice.TotalAmount, invoice.DiscountAmount, invoice.PaidAmount);
                invoice.RemainingAmount = netAmountDue - invoice.PaidAmount;

                if (partyMode == SalesInvoicePartyMode.WalkInCustomer && invoice.RemainingAmount != 0)
                {
                    throw new InvalidOperationException("فاتورة العميل النقدي يجب أن تكون مسددة بالكامل. إذا كان هناك متبقي، سجّل العميل أولاً.");
                }

                await _context.SalesInvoices.AddAsync(invoice, cancellationToken);

                // Update Client Balance Identical to Legacy
                if (client != null)
                {
                    client.Balance += invoice.RemainingAmount;
                }

                // 2. SAVE CHANGES (Applies concurrent RowVersion collision checks atomically)
                await _context.SaveChangesAsync(cancellationToken);

                // 3. COMMIT ONLY AT THE END If SaveChanges succeeded
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("[SalesInvoice:MediatR] Transaction {TxId} COMMITTED successfully. Invoice={InvNumber}", transactionId, invoice.InvoiceNumber);
                return _mapper.Map<SalesInvoiceViewModel>(invoice);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "[SalesInvoice:MediatR] Transaction {TxId} ABORTED: Missing RowVersion. Concurrency Collision Detected.", transactionId);
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SalesInvoice:MediatR] Transaction {TxId} FAILED. Rolling back changes. Error: {Message}", transactionId, ex.Message);
                // 4. EXPLICIT ROLLBACK ON ANY ERROR (Prevents partial-updates)
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private static void ValidateTotals(decimal totalAmount, decimal discountAmount, decimal paidAmount)
        {
            if (discountAmount > totalAmount)
                throw new InvalidOperationException("مبلغ الخصم لا يمكن أن يكون أكبر من إجمالي الفاتورة.");

            var netAmountDue = totalAmount - discountAmount;
            if (paidAmount > netAmountDue)
                throw new InvalidOperationException($"المبلغ المدفوع لا يمكن أن يكون أكبر من صافي الفاتورة. الصافي: {netAmountDue:N2}.");
        }

        private static SalesInvoicePartyMode ResolvePartyMode(SalesInvoiceCreateModel model)
        {
            if (model.PartyMode == SalesInvoicePartyMode.RegisteredClient)
            {
                if (!model.ClientId.HasValue || model.ClientId.Value <= 0)
                    throw new InvalidOperationException("يجب اختيار العميل.");

                return SalesInvoicePartyMode.RegisteredClient;
            }

            if (model.PartyMode == SalesInvoicePartyMode.WalkInCustomer)
            {
                if (string.IsNullOrWhiteSpace(model.OneTimeCustomerName))
                    throw new InvalidOperationException("يجب إدخال اسم العميل النقدي.");

                return SalesInvoicePartyMode.WalkInCustomer;
            }

            throw new InvalidOperationException("نوع العميل غير صالح.");
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
