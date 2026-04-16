using AutoMapper;
using MaterialManagement.BLL.ModelVM.Reports;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class ReportService : IReportService
    {
        private readonly MaterialManagementContext _context;
        private readonly IMapper _mapper;
        public ReportService(MaterialManagementContext context,IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<List<AccountStatementViewModel>> GetClientAccountStatementAsync(int clientId, DateTime? fromDate, DateTime? toDate)
        {
            var client = await _context.Clients
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return new List<AccountStatementViewModel>();

            DateTime finalFromDate = fromDate ?? DateTime.MinValue;
            DateTime finalToDate = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Now.Date.AddDays(1).AddTicks(-1);

            var salesInPeriod = await _context.SalesInvoices
                .IgnoreQueryFilters()
                .Include(i => i.SalesInvoiceItems).ThenInclude(item => item.Material)
                .Where(i => i.ClientId == clientId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate && i.IsActive)
                .ToListAsync();

            var paymentsInPeriod = await _context.ClientPayments
                .IgnoreQueryFilters()
                .Include(p => p.SalesInvoice)
                .Where(p => p.ClientId == clientId && p.PaymentDate >= finalFromDate && p.PaymentDate <= finalToDate)
                .ToListAsync();

            var returnsInPeriod = await _context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Include(i => i.PurchaseInvoiceItems).ThenInclude(item => item.Material)
                .Where(i => i.ClientId == clientId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate && i.IsActive)
                .ToListAsync();

            var salesReturnsInPeriod = await _context.SalesReturns
                .IgnoreQueryFilters()
                .Include(r => r.SalesReturnItems).ThenInclude(item => item.Material)
                .Where(r =>
                    r.ClientId == clientId &&
                    r.ReturnDate >= finalFromDate &&
                    r.ReturnDate <= finalToDate &&
                    r.IsActive &&
                    r.Status == ReturnStatus.Posted)
                .ToListAsync();

            var salesInvoiceIds = salesInPeriod.Select(i => i.Id).ToList();
            var linkedPaymentsByInvoice = salesInvoiceIds.Any()
                ? await _context.ClientPayments
                    .IgnoreQueryFilters()
                    .Where(p => p.SalesInvoiceId.HasValue && salesInvoiceIds.Contains(p.SalesInvoiceId.Value))
                    .GroupBy(p => p.SalesInvoiceId.GetValueOrDefault())
                    .Select(group => new { SalesInvoiceId = group.Key, Amount = group.Sum(p => p.Amount) })
                    .ToDictionaryAsync(x => x.SalesInvoiceId, x => x.Amount)
                : new Dictionary<int, decimal>();

            decimal totalDebitInPeriod = salesInPeriod.Sum(CalculateSalesInvoiceNet);
            decimal totalCreditInPeriod =
                salesInPeriod.Sum(i => ReconstructInitialPaid(i.PaidAmount, GetPaymentTotal(linkedPaymentsByInvoice, i.Id))) +
                paymentsInPeriod.Sum(p => p.Amount) +
                returnsInPeriod.Sum(i => i.RemainingAmount) +
                salesReturnsInPeriod.Sum(r => r.TotalNetAmount);

            decimal netChangeInPeriod = totalDebitInPeriod - totalCreditInPeriod;
            decimal openingBalance = client.Balance - netChangeInPeriod;

            var allTransactions = new List<AccountStatementTransaction>();

            foreach (var invoice in salesInPeriod)
            {
                var invoiceNet = CalculateSalesInvoiceNet(invoice);
                var initialPaid = ReconstructInitialPaid(invoice.PaidAmount, GetPaymentTotal(linkedPaymentsByInvoice, invoice.Id));

                allTransactions.Add(new AccountStatementTransaction
                {
                    Date = invoice.InvoiceDate,
                    Type = "فاتورة بيع",
                    CauseLabel = "فاتورة بيع",
                    Description = BuildInvoiceDescription(
                        "فاتورة بيع",
                        invoice.InvoiceNumber,
                        invoiceNet,
                        initialPaid),
                    EffectLabel = BuildMovementEffectLabel(invoiceNet, initialPaid),
                    Ref = invoice.InvoiceNumber,
                    DocId = (int?)invoice.Id,
                    DocType = "SalesInvoice",
                    Debit = invoiceNet,
                    Credit = initialPaid,
                    Items = MapSalesInvoiceItems(invoice.SalesInvoiceItems)
                });
            }

            allTransactions.AddRange(paymentsInPeriod.Select(p => new AccountStatementTransaction
            {
                Date = p.PaymentDate,
                Type = "تحصيل",
                CauseLabel = "تحصيل من عميل",
                Description = BuildPaymentDescription("تحصيل من العميل", p.Amount, p.PaymentMethod, p.Notes, p.SalesInvoice?.InvoiceNumber),
                EffectLabel = "تخفيض الرصيد",
                Ref = "دفعة #" + p.Id,
                DocId = p.Id,
                DocType = "ClientPayment",
                Debit = 0m,
                Credit = p.Amount,
                Items = new List<TransactionItemViewModel>()
            }));

            allTransactions.AddRange(returnsInPeriod.Select(i => new AccountStatementTransaction
            {
                Date = i.InvoiceDate,
                Type = "مرتجع بيع",
                CauseLabel = "مرتجع من عميل",
                Description = $"مرتجع أصناف بقيمة {i.RemainingAmount:N2}",
                EffectLabel = "تخفيض الرصيد",
                Ref = i.InvoiceNumber,
                DocId = (int?)i.Id,
                DocType = "PurchaseInvoice",
                Debit = 0m,
                Credit = i.RemainingAmount,
                Items = MapPurchaseInvoiceItems(i.PurchaseInvoiceItems)
            }));

            allTransactions.AddRange(salesReturnsInPeriod.Select(r => new AccountStatementTransaction
            {
                Date = r.ReturnDate,
                Type = "مرتجع بيع",
                CauseLabel = "مرتجع بيع",
                Description = $"مرتجع بيع بقيمة {r.TotalNetAmount:N2}",
                EffectLabel = "تخفيض الرصيد",
                Ref = r.ReturnNumber,
                DocId = (int?)r.Id,
                DocType = "SalesReturn",
                Debit = 0m,
                Credit = r.TotalNetAmount,
                Items = MapSalesReturnItems(r.SalesReturnItems)
            }));

            var sortedTransactions = allTransactions.OrderBy(t => t.Date).ThenBy(t => t.Ref).ToList();
            var openingRowDate = fromDate ?? sortedTransactions.FirstOrDefault()?.Date ?? DateTime.Today;

            var statement = new List<AccountStatementViewModel>();
            decimal currentBalance = openingBalance;

            statement.Add(CreateOpeningBalanceRow(openingRowDate, openingBalance, isClient: true));

            currentBalance = openingBalance;

            foreach (var trans in sortedTransactions)
            {
                currentBalance = currentBalance + trans.Debit - trans.Credit;
                statement.Add(new AccountStatementViewModel
                {
                    TransactionDate = trans.Date,
                    TransactionType = trans.Type,
                    CauseLabel = trans.CauseLabel,
                    Description = trans.Description,
                    EffectLabel = trans.EffectLabel,
                    Reference = trans.Ref,
                    DocumentId = trans.DocId,
                    DocumentType = trans.DocType,
                    Debit = trans.Debit,
                    Credit = trans.Credit,
                    Balance = currentBalance,
                    Items = trans.Items
                });
            }

            return statement;
        }


        public async Task<List<AccountStatementViewModel>> GetSupplierAccountStatementAsync(int supplierId, DateTime? fromDate, DateTime? toDate)
        {
            DateTime finalFromDate = fromDate ?? DateTime.MinValue;
            DateTime finalToDate = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Now.Date.AddDays(1).AddTicks(-1);

            var invoicesInPeriod = await _context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Include(i => i.PurchaseInvoiceItems).ThenInclude(item => item.Material)
                .Where(i => i.SupplierId == supplierId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate && i.IsActive)
                .ToListAsync();

            var paymentsInPeriod = await _context.SupplierPayments
                .IgnoreQueryFilters()
                .Include(p => p.PurchaseInvoice)
                .Where(p => p.SupplierId == supplierId && p.PaymentDate >= finalFromDate && p.PaymentDate <= finalToDate)
                .ToListAsync();

            var purchaseInvoiceIds = invoicesInPeriod.Select(i => i.Id).ToList();
            var linkedPaymentsByInvoice = purchaseInvoiceIds.Any()
                ? await _context.SupplierPayments
                    .IgnoreQueryFilters()
                    .Where(p => p.PurchaseInvoiceId.HasValue && purchaseInvoiceIds.Contains(p.PurchaseInvoiceId.Value))
                    .GroupBy(p => p.PurchaseInvoiceId.GetValueOrDefault())
                    .Select(group => new { PurchaseInvoiceId = group.Key, Amount = group.Sum(p => p.Amount) })
                    .ToDictionaryAsync(x => x.PurchaseInvoiceId, x => x.Amount)
                : new Dictionary<int, decimal>();

            decimal totalDebitInPeriod = invoicesInPeriod.Sum(CalculatePurchaseInvoiceNet);
            decimal totalCreditInPeriod =
                invoicesInPeriod.Sum(i => ReconstructInitialPaid(i.PaidAmount, GetPaymentTotal(linkedPaymentsByInvoice, i.Id))) +
                paymentsInPeriod.Sum(p => p.Amount);

            decimal netChangeInPeriod = totalDebitInPeriod - totalCreditInPeriod;

            var supplier = await _context.Suppliers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == supplierId);
            if (supplier == null) return new List<AccountStatementViewModel>();

            decimal openingBalance = supplier.Balance - netChangeInPeriod;

            var allTransactions = new List<AccountStatementTransaction>();

            foreach (var invoice in invoicesInPeriod)
            {
                var invoiceNet = CalculatePurchaseInvoiceNet(invoice);
                var initialPaid = ReconstructInitialPaid(invoice.PaidAmount, GetPaymentTotal(linkedPaymentsByInvoice, invoice.Id));

                allTransactions.Add(new AccountStatementTransaction
                {
                    Date = invoice.InvoiceDate,
                    Type = "فاتورة شراء",
                    CauseLabel = "فاتورة شراء",
                    Description = BuildInvoiceDescription(
                        "فاتورة شراء",
                        invoice.InvoiceNumber,
                        invoiceNet,
                        initialPaid),
                    EffectLabel = BuildMovementEffectLabel(invoiceNet, initialPaid),
                    Ref = invoice.InvoiceNumber,
                    DocId = (int?)invoice.Id,
                    DocType = "PurchaseInvoice",
                    Debit = invoiceNet,
                    Credit = initialPaid,
                    Items = MapPurchaseInvoiceItems(invoice.PurchaseInvoiceItems)
                });
            }

            allTransactions.AddRange(paymentsInPeriod.Select(t => new AccountStatementTransaction
            {
                Date = t.PaymentDate,
                Type = "دفعة لمورد",
                CauseLabel = "دفعة لمورد",
                Description = BuildPaymentDescription("دفعة للمورد", t.Amount, t.PaymentMethod, t.Notes, t.PurchaseInvoice?.InvoiceNumber),
                EffectLabel = "تخفيض الرصيد",
                Ref = "دفعة #" + t.Id,
                DocId = t.Id,
                DocType = "SupplierPayment",
                Debit = 0m,
                Credit = t.Amount,
                Items = new List<TransactionItemViewModel>()
            }));

            var sortedTransactions = allTransactions.OrderBy(t => t.Date).ThenBy(t => t.Ref).ToList();
            var openingRowDate = fromDate ?? sortedTransactions.FirstOrDefault()?.Date ?? DateTime.Today;

            var statement = new List<AccountStatementViewModel>();
            decimal currentBalance = openingBalance;

            statement.Add(CreateOpeningBalanceRow(openingRowDate, openingBalance, isClient: false));

            currentBalance = openingBalance;

            foreach (var trans in sortedTransactions)
            {
                currentBalance = currentBalance + trans.Debit - trans.Credit;
                statement.Add(new AccountStatementViewModel
                {
                    TransactionDate = trans.Date,
                    TransactionType = trans.Type,
                    CauseLabel = trans.CauseLabel,
                    Description = trans.Description,
                    EffectLabel = trans.EffectLabel,
                    Reference = trans.Ref,
                    DocumentId = trans.DocId,
                    DocumentType = trans.DocType,
                    Debit = trans.Debit,
                    Credit = trans.Credit,
                    Balance = currentBalance,
                    Items = trans.Items
                });
            }

            return statement;
        }
        public async Task<List<MaterialMovementViewModel>> GetMaterialMovementAsync(int materialId, DateTime? fromDate, DateTime? toDate)
        {
            // === 1. تحديد نطاق البحث النهائي ===
            // إذا لم يتم تحديد تاريخ بداية، نستخدم أقدم تاريخ ممكن
            DateTime finalFromDate = fromDate ?? DateTime.MinValue;
            // إذا لم يتم تحديد تاريخ نهاية، نستخدم تاريخ اليوم (ونشمل اليوم بأكمله)
            DateTime finalToDate = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Now.Date.AddDays(1).AddTicks(-1);

            // === 2. حساب الرصيد الافتتاحي (يُحسب دائماً حتى finalFromDate) ===

            // المشتريات قبل الفترة
            var purchasesBefore = await _context.PurchaseInvoiceItems
                .IgnoreQueryFilters()
                .Where(i => i.MaterialId == materialId && i.PurchaseInvoice.IsActive && i.PurchaseInvoice.SupplierId.HasValue && i.PurchaseInvoice.InvoiceDate < finalFromDate)
                .SumAsync(i => (decimal?)i.Quantity) ?? 0;

            // مرتجعات البيع (تُضاف للمخزون) قبل الفترة
            var returnsBefore = await _context.PurchaseInvoiceItems
                .IgnoreQueryFilters()
                .Where(i => i.MaterialId == materialId && i.PurchaseInvoice.IsActive && i.PurchaseInvoice.ClientId.HasValue && i.PurchaseInvoice.InvoiceDate < finalFromDate)
                .SumAsync(i => (decimal?)i.Quantity) ?? 0;

            // المبيعات قبل الفترة
            var salesBefore = await _context.SalesInvoiceItems
                .IgnoreQueryFilters()
                .Where(i => i.MaterialId == materialId && i.SalesInvoice.IsActive && i.SalesInvoice.InvoiceDate < finalFromDate)
                .SumAsync(i => (decimal?)i.Quantity) ?? 0;

            var salesReturnsBefore = await _context.SalesReturnItems
                .IgnoreQueryFilters()
                .Where(i =>
                    i.MaterialId == materialId &&
                    i.SalesReturn.IsActive &&
                    i.SalesReturn.Status == ReturnStatus.Posted &&
                    i.SalesReturn.ReturnDate < finalFromDate)
                .SumAsync(i => (decimal?)i.ReturnedQuantity) ?? 0;

            decimal openingBalance = (purchasesBefore + returnsBefore + salesReturnsBefore) - salesBefore;

            // === 3. جلب الحركات خلال الفترة المحددة ===

            // المشتريات ومرتجعات البيع (وارد)
            var purchasesInPeriod = await _context.PurchaseInvoiceItems
                .IgnoreQueryFilters()
                .Include(i => i.PurchaseInvoice.Supplier)
                .Include(i => i.PurchaseInvoice.Client)
                .Where(i => i.MaterialId == materialId && i.PurchaseInvoice.IsActive && i.PurchaseInvoice.InvoiceDate >= finalFromDate && i.PurchaseInvoice.InvoiceDate <= finalToDate)
                .Select(i => new {
                    Date = i.PurchaseInvoice.InvoiceDate,
                    Type = i.PurchaseInvoice.ClientId.HasValue ? "مرتجع من عميل" : "شراء من مورد",
                    Ref = i.PurchaseInvoice.InvoiceNumber,
                    Qty = i.Quantity,
                    IsIn = true,
                    Source = i.PurchaseInvoice.ClientId.HasValue
                        ? i.PurchaseInvoice.Client.Name
                        : (i.PurchaseInvoice.Supplier != null ? i.PurchaseInvoice.Supplier.Name : (i.PurchaseInvoice.OneTimeSupplierName ?? "مورد يدوي"))
                }).ToListAsync();

            var salesInPeriod = await _context.SalesInvoiceItems
                .IgnoreQueryFilters()
                .Include(i => i.SalesInvoice.Client)
                .Where(i => i.MaterialId == materialId && i.SalesInvoice.IsActive && i.SalesInvoice.InvoiceDate >= finalFromDate && i.SalesInvoice.InvoiceDate <= finalToDate)
                .Select(i => new {
                    Date = i.SalesInvoice.InvoiceDate,
                    Type = "بيع لعميل",
                    Ref = i.SalesInvoice.InvoiceNumber,
                    Qty = i.Quantity,
                    IsIn = false,
                    Source = i.SalesInvoice.Client != null ? i.SalesInvoice.Client.Name : (i.SalesInvoice.OneTimeCustomerName ?? "عميل نقدي")
                }).ToListAsync();

            var salesReturnsInPeriod = await _context.SalesReturnItems
                .IgnoreQueryFilters()
                .Include(i => i.SalesReturn.Client)
                .Where(i =>
                    i.MaterialId == materialId &&
                    i.SalesReturn.IsActive &&
                    i.SalesReturn.Status == ReturnStatus.Posted &&
                    i.SalesReturn.ReturnDate >= finalFromDate &&
                    i.SalesReturn.ReturnDate <= finalToDate)
                .Select(i => new {
                    Date = i.SalesReturn.ReturnDate,
                    Type = "مرتجع بيع",
                    Ref = i.SalesReturn.ReturnNumber,
                    Qty = i.ReturnedQuantity,
                    IsIn = true,
                    Source = i.SalesReturn.Client.Name
                }).ToListAsync();


            var allTransactions = purchasesInPeriod.Concat(salesInPeriod)
                                                   .Concat(salesReturnsInPeriod)
                                                   .OrderBy(t => t.Date)
                                                   .ToList();

            var report = new List<MaterialMovementViewModel>();
            decimal currentBalance = openingBalance;


            report.Add(new MaterialMovementViewModel { TransactionDate = finalFromDate, TransactionType = "رصيد افتتاحي", Balance = currentBalance, Source = "" });


            foreach (var trans in allTransactions)
            {
                currentBalance += trans.IsIn ? trans.Qty : -trans.Qty;
                report.Add(new MaterialMovementViewModel
                {
                    TransactionDate = trans.Date,
                    TransactionType = trans.Type,
                    InvoiceNumber = trans.Ref,
                    Source = trans.Source,
                    QuantityIn = trans.IsIn ? trans.Qty : 0,
                    QuantityOut = !trans.IsIn ? trans.Qty : 0,
                    Balance = currentBalance
                });
            }


            if (!fromDate.HasValue && !toDate.HasValue)
            {

                return report.TakeLast(11).ToList();
            }

            return report;
        }

        public async Task<List<ProfitReportViewModel>> GetProfitReportAsync(DateTime fromDate, DateTime toDate)
        {

            var inclusiveToDate = toDate.Date.AddDays(1).AddTicks(-1);

            var salesInvoices = await _context.SalesInvoices
                .IgnoreQueryFilters()
                .Include(i => i.Client)
                .Include(i => i.SalesInvoiceItems).ThenInclude(item => item.Material)
                .Where(i => i.IsActive && i.InvoiceDate >= fromDate && i.InvoiceDate <= inclusiveToDate).ToListAsync();

            var salesReturns = await _context.SalesReturns
                .IgnoreQueryFilters()
                .Include(r => r.Client)
                .Include(r => r.SalesReturnItems).ThenInclude(item => item.Material)
                .Where(r =>
                    r.IsActive &&
                    r.Status == ReturnStatus.Posted &&
                    r.ReturnDate >= fromDate &&
                    r.ReturnDate <= inclusiveToDate)
                .ToListAsync();

            var report = new List<ProfitReportViewModel>();

            foreach (var invoice in salesInvoices)
            {
                decimal totalCost = invoice.SalesInvoiceItems.Sum(item => item.Quantity * (item.Material.PurchasePrice ?? 0));
                decimal netRevenue = CalculateSalesInvoiceNet(invoice);
                decimal profit = netRevenue - totalCost;
                report.Add(new ProfitReportViewModel
                {
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    ClientName = invoice.Client != null ? invoice.Client.Name : (invoice.OneTimeCustomerName ?? "عميل نقدي"),
                    TotalAmount = netRevenue,
                    TotalCost = totalCost,
                    Profit = profit
                });
            }

            foreach (var salesReturn in salesReturns)
            {
                decimal returnedCost = salesReturn.SalesReturnItems.Sum(item => item.ReturnedQuantity * (item.Material.PurchasePrice ?? 0));
                decimal returnedRevenue = salesReturn.TotalNetAmount;
                report.Add(new ProfitReportViewModel
                {
                    InvoiceNumber = salesReturn.ReturnNumber,
                    InvoiceDate = salesReturn.ReturnDate,
                    ClientName = salesReturn.Client.Name,
                    TotalAmount = -returnedRevenue,
                    TotalCost = -returnedCost,
                    Profit = -returnedRevenue + returnedCost
                });
            }
            return report.OrderBy(r => r.InvoiceDate).ToList();
        }

        private static AccountStatementViewModel CreateOpeningBalanceRow(DateTime transactionDate, decimal openingBalance, bool isClient)
        {
            var isPositive = openingBalance > 0;
            var isNegative = openingBalance < 0;
            var ownerText = isClient ? "العميل" : "المورد";
            var positiveMeaning = isClient ? "رصيد سابق مستحق على العميل" : "رصيد سابق مستحق للمورد";
            var negativeMeaning = isClient ? "رصيد سابق لصالح العميل" : "رصيد سابق لصالح المنشأة";

            return new AccountStatementViewModel
            {
                TransactionDate = transactionDate,
                TransactionType = isPositive ? "رصيد افتتاحي (مستحق)" : isNegative ? "رصيد افتتاحي (لصالح الحساب)" : "رصيد افتتاحي (صفر)",
                CauseLabel = "رصيد سابق",
                Description = isPositive ? positiveMeaning : isNegative ? negativeMeaning : $"لا يوجد رصيد سابق على {ownerText}",
                EffectLabel = "رصيد البداية",
                Reference = "رصيد سابق",
                DocumentType = "OpeningBalance",
                Debit = isPositive ? openingBalance : 0m,
                Credit = isNegative ? Math.Abs(openingBalance) : 0m,
                Balance = openingBalance
            };
        }

        private static string BuildInvoiceDescription(string invoiceType, string invoiceNumber, decimal invoiceNet, decimal initialPaid)
        {
            var description = $"صافي {invoiceType} بقيمة {invoiceNet:N2}";
            if (initialPaid > 0)
            {
                description += $" | مدفوع عند الإنشاء {initialPaid:N2}";
            }

            return description;
        }

        private static string BuildPaymentDescription(string paymentType, decimal amount, string? paymentMethod, string? notes, string? linkedInvoiceNumber)
        {
            var parts = new List<string> { $"{paymentType} بقيمة {amount:N2}" };

            if (!string.IsNullOrWhiteSpace(paymentMethod))
            {
                parts.Add(paymentMethod);
            }

            if (!string.IsNullOrWhiteSpace(linkedInvoiceNumber))
            {
                parts.Add($"فاتورة {linkedInvoiceNumber}");
            }

            if (!string.IsNullOrWhiteSpace(notes))
            {
                parts.Add(notes);
            }

            return string.Join(" | ", parts);
        }

        private static string BuildMovementEffectLabel(decimal debit, decimal credit)
        {
            if (debit > 0 && credit > 0)
            {
                return "زيادة مع دفعة";
            }

            if (debit > 0)
            {
                return "زيادة الرصيد";
            }

            if (credit > 0)
            {
                return "تخفيض الرصيد";
            }

            return "بدون أثر";
        }

        private static decimal CalculateSalesInvoiceNet(SalesInvoice invoice)
        {
            return invoice.TotalAmount - invoice.DiscountAmount;
        }

        private static decimal CalculatePurchaseInvoiceNet(PurchaseInvoice invoice)
        {
            return invoice.TotalAmount;
        }

        private static decimal ReconstructInitialPaid(decimal cumulativePaidAmount, decimal linkedPaymentRowsTotal)
        {
            // Report-only best-effort reconstruction. Do not mutate stored data or redistribute the difference.
            return Math.Max(0m, cumulativePaidAmount - linkedPaymentRowsTotal);
        }

        private static decimal GetPaymentTotal(IReadOnlyDictionary<int, decimal> paymentTotals, int invoiceId)
        {
            return paymentTotals.TryGetValue(invoiceId, out var total)
                ? total
                : 0m;
        }

        private List<TransactionItemViewModel> MapSalesInvoiceItems(IEnumerable<SalesInvoiceItem> items)
        {
            return _mapper.Map<List<TransactionItemViewModel>>(items);
        }

        private List<TransactionItemViewModel> MapPurchaseInvoiceItems(IEnumerable<PurchaseInvoiceItem> items)
        {
            return _mapper.Map<List<TransactionItemViewModel>>(items);
        }

        private static List<TransactionItemViewModel> MapSalesReturnItems(IEnumerable<SalesReturnItem> items)
        {
            return items.Select(item => new TransactionItemViewModel
            {
                MaterialName = item.Material?.Name ?? string.Empty,
                Quantity = item.ReturnedQuantity,
                Unit = item.Material?.Unit ?? string.Empty,
                UnitPrice = item.NetUnitPrice
            }).ToList();
        }

        private sealed class AccountStatementTransaction
        {
            public DateTime Date { get; init; }
            public string Type { get; init; } = string.Empty;
            public string CauseLabel { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public string EffectLabel { get; init; } = string.Empty;
            public string Ref { get; init; } = string.Empty;
            public int? DocId { get; init; }
            public string? DocType { get; init; }
            public decimal Debit { get; init; }
            public decimal Credit { get; init; }
            public List<TransactionItemViewModel> Items { get; init; } = new List<TransactionItemViewModel>();
        }

    }
}
