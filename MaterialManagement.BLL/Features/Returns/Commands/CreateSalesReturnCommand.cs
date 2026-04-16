using FluentValidation;
using MaterialManagement.BLL.ModelVM.Returns;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Features.Returns.Commands
{
    public class CreateSalesReturnCommand : IRequest<SalesReturnViewModel>
    {
        public SalesReturnCreateModel Model { get; set; } = new();
    }

    public class CreateSalesReturnValidator : AbstractValidator<CreateSalesReturnCommand>
    {
        public CreateSalesReturnValidator()
        {
            RuleFor(x => x.Model)
                .NotNull().WithMessage("بيانات مرتجع البيع مطلوبة.");

            When(x => x.Model != null, () =>
            {
                RuleFor(x => x.Model.SalesInvoiceId)
                    .GreaterThan(0).WithMessage("يجب اختيار فاتورة البيع.");

                RuleFor(x => x.Model.Items)
                    .NotEmpty().WithMessage("يجب إضافة بند واحد على الأقل لمرتجع البيع.");

                RuleFor(x => x.Model.Items)
                    .Must(HaveUniqueInvoiceItemIds)
                    .WithMessage("لا يمكن تكرار نفس بند الفاتورة في مرتجع البيع.");

                RuleForEach(x => x.Model.Items).ChildRules(items =>
                {
                    items.RuleFor(i => i.SalesInvoiceItemId)
                        .GreaterThan(0).WithMessage("يجب اختيار بند صحيح من فاتورة البيع.");

                    items.RuleFor(i => i.ReturnedQuantity)
                        .GreaterThan(0).WithMessage("كمية المرتجع يجب أن تكون أكبر من الصفر.");
                });
            });
        }

        private static bool HaveUniqueInvoiceItemIds(List<SalesReturnItemCreateModel>? items)
        {
            if (items == null)
            {
                return true;
            }

            return items.Select(i => i.SalesInvoiceItemId).Distinct().Count() == items.Count;
        }
    }

    public class CreateSalesReturnHandler : IRequestHandler<CreateSalesReturnCommand, SalesReturnViewModel>
    {
        private readonly MaterialManagementContext _context;
        private readonly ILogger<CreateSalesReturnHandler> _logger;

        public CreateSalesReturnHandler(
            MaterialManagementContext context,
            ILogger<CreateSalesReturnHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SalesReturnViewModel> Handle(CreateSalesReturnCommand request, CancellationToken cancellationToken)
        {
            var validator = new CreateSalesReturnValidator();
            await validator.ValidateAndThrowAsync(request, cancellationToken);

            var transactionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _logger.LogInformation("[SalesReturn:MediatR] Transaction {TxId} STARTED.", transactionId);

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var model = request.Model;

                var invoice = await _context.SalesInvoices
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(i => i.Id == model.SalesInvoiceId, cancellationToken);

                if (invoice == null)
                {
                    throw new InvalidOperationException("فاتورة البيع غير موجودة.");
                }

                if (!invoice.IsActive)
                {
                    throw new InvalidOperationException("لا يمكن إنشاء مرتجع بيع لفاتورة محذوفة أو غير نشطة.");
                }

                if (!invoice.ClientId.HasValue)
                {
                    throw new InvalidOperationException("لا يمكن إنشاء مرتجع بيع لفاتورة عميل نقدي غير مسجل.");
                }

                var client = await _context.Clients
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.Id == invoice.ClientId.Value, cancellationToken);

                if (client == null || !client.IsActive)
                {
                    throw new InvalidOperationException("لا يمكن إنشاء مرتجع بيع لأن العميل غير موجود أو غير نشط.");
                }

                if (invoice.TotalAmount <= 0)
                {
                    throw new InvalidOperationException("لا يمكن إنشاء مرتجع لفاتورة إجماليها غير صالح.");
                }

                if (invoice.DiscountAmount < 0 || invoice.DiscountAmount > invoice.TotalAmount)
                {
                    throw new InvalidOperationException("قيمة الخصم المسجلة على الفاتورة غير صالحة.");
                }

                var requestedItemIds = model.Items.Select(i => i.SalesInvoiceItemId).ToList();
                var invoiceItems = await _context.SalesInvoiceItems
                    .IgnoreQueryFilters()
                    .Where(i => requestedItemIds.Contains(i.Id))
                    .ToListAsync(cancellationToken);

                if (invoiceItems.Count != requestedItemIds.Count)
                {
                    throw new InvalidOperationException("توجد بنود غير موجودة في فاتورة البيع المحددة.");
                }

                if (invoiceItems.Any(i => i.SalesInvoiceId != invoice.Id))
                {
                    throw new InvalidOperationException("لا يمكن إرجاع بند لا ينتمي إلى فاتورة البيع المحددة.");
                }

                var materialIds = invoiceItems.Select(i => i.MaterialId).Distinct().ToList();
                var materials = await _context.Materials
                    .IgnoreQueryFilters()
                    .Where(m => materialIds.Contains(m.Id))
                    .ToDictionaryAsync(m => m.Id, cancellationToken);

                if (materials.Count != materialIds.Count)
                {
                    throw new InvalidOperationException("توجد مادة مرتبطة ببند فاتورة غير موجودة.");
                }

                var discountRatio = invoice.DiscountAmount / invoice.TotalAmount;
                var invoiceItemsById = invoiceItems.ToDictionary(i => i.Id);

                var salesReturn = new SalesReturn
                {
                    ReturnNumber = $"SR-{DateTime.Now.Ticks}",
                    SalesInvoiceId = invoice.Id,
                    ClientId = invoice.ClientId.Value,
                    ReturnDate = model.ReturnDate == default ? DateTime.Now : model.ReturnDate,
                    Status = ReturnStatus.Posted,
                    Notes = model.Notes,
                    IsActive = true
                };

                decimal totalGrossAmount = 0;
                decimal totalNetAmount = 0;

                foreach (var itemModel in model.Items)
                {
                    var invoiceItem = invoiceItemsById[itemModel.SalesInvoiceItemId];
                    var material = materials[invoiceItem.MaterialId];

                    var alreadyReturnedQuantity = await _context.SalesReturnItems
                        .IgnoreQueryFilters()
                        .Where(ri =>
                            ri.SalesInvoiceItemId == invoiceItem.Id &&
                            ri.SalesReturn.IsActive &&
                            ri.SalesReturn.Status == ReturnStatus.Posted)
                        .SumAsync(ri => (decimal?)ri.ReturnedQuantity, cancellationToken) ?? 0m;

                    var remainingQuantity = invoiceItem.Quantity - alreadyReturnedQuantity;
                    if (itemModel.ReturnedQuantity > remainingQuantity)
                    {
                        throw new InvalidOperationException(
                            $"لا يمكن إرجاع كمية أكبر من الكمية المتبقية للبند: {material.Name}. الكمية المتبقية: {remainingQuantity:N2}.");
                    }

                    var grossLineAmount = RoundMoney(itemModel.ReturnedQuantity * invoiceItem.UnitPrice);
                    var netUnitPrice = RoundMoney(invoiceItem.UnitPrice * (1 - discountRatio));
                    var netLineAmount = RoundMoney(itemModel.ReturnedQuantity * netUnitPrice);

                    material.Quantity += itemModel.ReturnedQuantity;

                    salesReturn.SalesReturnItems.Add(new SalesReturnItem
                    {
                        SalesInvoiceItemId = invoiceItem.Id,
                        MaterialId = invoiceItem.MaterialId,
                        ReturnedQuantity = itemModel.ReturnedQuantity,
                        OriginalUnitPrice = invoiceItem.UnitPrice,
                        NetUnitPrice = netUnitPrice,
                        TotalReturnNetAmount = netLineAmount
                    });

                    totalGrossAmount += grossLineAmount;
                    totalNetAmount += netLineAmount;
                }

                salesReturn.TotalGrossAmount = RoundMoney(totalGrossAmount);
                salesReturn.TotalNetAmount = RoundMoney(totalNetAmount);
                salesReturn.TotalProratedDiscount = RoundMoney(salesReturn.TotalGrossAmount - salesReturn.TotalNetAmount);

                client.Balance -= salesReturn.TotalNetAmount;

                await _context.SalesReturns.AddAsync(salesReturn, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "[SalesReturn:MediatR] Transaction {TxId} COMMITTED successfully. Return={ReturnNumber}",
                    transactionId,
                    salesReturn.ReturnNumber);

                return ToViewModel(salesReturn, materials);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "[SalesReturn:MediatR] Transaction {TxId} ABORTED: concurrency collision detected.",
                    transactionId);
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[SalesReturn:MediatR] Transaction {TxId} FAILED. Rolling back changes. Error: {Message}",
                    transactionId,
                    ex.Message);
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private static SalesReturnViewModel ToViewModel(SalesReturn salesReturn, IReadOnlyDictionary<int, Material> materials)
        {
            return new SalesReturnViewModel
            {
                Id = salesReturn.Id,
                ReturnNumber = salesReturn.ReturnNumber,
                SalesInvoiceId = salesReturn.SalesInvoiceId,
                ClientId = salesReturn.ClientId,
                ReturnDate = salesReturn.ReturnDate,
                Status = salesReturn.Status,
                TotalGrossAmount = salesReturn.TotalGrossAmount,
                TotalProratedDiscount = salesReturn.TotalProratedDiscount,
                TotalNetAmount = salesReturn.TotalNetAmount,
                Notes = salesReturn.Notes,
                Items = salesReturn.SalesReturnItems.Select(item =>
                {
                    materials.TryGetValue(item.MaterialId, out var material);
                    return new SalesReturnItemViewModel
                    {
                        Id = item.Id,
                        SalesInvoiceItemId = item.SalesInvoiceItemId,
                        MaterialId = item.MaterialId,
                        MaterialCode = material?.Code,
                        MaterialName = material?.Name,
                        ReturnedQuantity = item.ReturnedQuantity,
                        OriginalUnitPrice = item.OriginalUnitPrice,
                        NetUnitPrice = item.NetUnitPrice,
                        TotalReturnNetAmount = item.TotalReturnNetAmount
                    };
                }).ToList()
            };
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
