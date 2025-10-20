using AutoMapper;
using MaterialManagement.BLL.ModelVM.Reports;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
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
        public ReportService(MaterialManagementContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<List<AccountStatementViewModel>> GetClientAccountStatementAsync(int clientId, DateTime? fromDate, DateTime? toDate)
        {
            var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return new List<AccountStatementViewModel>();
            // === 1. تحديد نطاق البحث الآمن ===
            DateTime finalFromDate = fromDate ?? DateTime.MinValue;
            DateTime finalToDate = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Now.Date.AddDays(1).AddTicks(-1);
            DateTime statementDisplayDate = fromDate ?? DateTime.Today;

            // === 2. جلب الحركات داخل الفترة المحددة أولاً ===

            // المبيعات في الفترة
            var salesInPeriod = await _context.SalesInvoices
                    .Include(i => i.SalesInvoiceItems).ThenInclude(item => item.Material) // <<< الخطوة 1: تضمين الأصناف
                    .Where(i => i.ClientId == clientId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate)
                    .Select(i => new {
                        Date = i.InvoiceDate,
                        Type = "فاتورة بيع",
                        Ref = i.InvoiceNumber,
                        DocId = (int?)i.Id,
                        DocType = "SalesInvoice",
                        Debit = i.TotalAmount,
                        Credit = 0m,
                        Items = i.SalesInvoiceItems
                    })
                    .ToListAsync();
            // التحصيلات في الفترة
            var paymentsInPeriod = await _context.ClientPayments
                .Where(p => p.ClientId == clientId && p.PaymentDate >= finalFromDate && p.PaymentDate <= finalToDate)
                .Select(p => new { Date = p.PaymentDate, Type = "تحصيل", Ref = "دفعة #" + p.Id, DocId = p.SalesInvoiceId, DocType = "ClientPayment", Debit = 0m, Credit = p.Amount })
                .ToListAsync();

            // المرتجعات في الفترة
            var returnsInPeriod = await _context.PurchaseInvoices
                    .Include(i => i.PurchaseInvoiceItems).ThenInclude(item => item.Material) // <<< تضمين أصناف المرتجعات
                    .Where(i => i.ClientId == clientId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate)
                    .Select(i => new { Date = i.InvoiceDate, Type = "مرتجع بيع", Ref = i.InvoiceNumber, DocId = (int?)i.Id, DocType = "PurchaseInvoice", Debit = 0m, Credit = i.TotalAmount, Items = i.PurchaseInvoiceItems })
                    .ToListAsync();


            decimal totalDebitInPeriod = salesInPeriod.Sum(t => t.Debit);
            decimal totalCreditInPeriod = paymentsInPeriod.Sum(t => t.Credit) + returnsInPeriod.Sum(t => t.Credit);
            decimal netChangeInPeriod = totalDebitInPeriod - totalCreditInPeriod;



            decimal openingBalance = client.Balance - netChangeInPeriod;


            var allTransactions = new List<dynamic>();
            allTransactions.AddRange(salesInPeriod);
            allTransactions.AddRange(paymentsInPeriod);
            allTransactions.AddRange(returnsInPeriod);
            var sortedTransactions = allTransactions.OrderBy(t => t.Date).ThenBy(t => t.Ref).ToList();


            var statement = new List<AccountStatementViewModel>();
            decimal currentBalance = openingBalance;


            if (openingBalance > 0)
            {
                statement.Add(new AccountStatementViewModel
                {
                    TransactionDate = statementDisplayDate,
                    TransactionType = "رصيد افتتاحي (مدين)",
                    Reference = "",
                    Debit = openingBalance,
                    Credit = 0m,
                    Balance = openingBalance
                });
            }
            else if (openingBalance < 0)
            {
                statement.Add(new AccountStatementViewModel
                {
                    TransactionDate = statementDisplayDate,
                    TransactionType = "رصيد افتتاحي (دائن)",
                    Reference = "",
                    Debit = 0m,
                    Credit = Math.Abs(openingBalance), // دائن
                    Balance = openingBalance
                });
            }
            else
            {
                statement.Add(new AccountStatementViewModel
                {
                    TransactionDate = statementDisplayDate,
                    TransactionType = "رصيد افتتاحي (صفر)",
                    Reference = "",
                    Balance = 0
                });
            }


            currentBalance = openingBalance;


            foreach (var trans in sortedTransactions)
            {
                currentBalance = currentBalance + trans.Debit - trans.Credit;
                statement.Add(new AccountStatementViewModel
                {
                    TransactionDate = trans.Date,
                    TransactionType = trans.Type,
                    Reference = trans.Ref,
                    DocumentId = trans.DocId,
                    DocumentType = trans.DocType,
                    Debit = trans.Debit,
                    Credit = trans.Credit,
                    Balance = currentBalance,
                    Items = trans.GetType().GetProperty("Items") != null
                    ? _mapper.Map<List<TransactionItemViewModel>>(trans.Items)
                    : new List<TransactionItemViewModel>()
                });
            }

            if (!fromDate.HasValue && !toDate.HasValue && statement.Count > 11)
            {
                return statement.TakeLast(11).ToList();
            }

            return statement;
        }



        public async Task<List<AccountStatementViewModel>> GetSupplierAccountStatementAsync(int supplierId, DateTime? fromDate, DateTime? toDate)
        {
            // === 1. تحديد نطاق البحث الآمن ===
            DateTime finalFromDate = fromDate ?? DateTime.MinValue;
            DateTime statementDisplayDate = fromDate ?? DateTime.Today;
            DateTime finalToDate = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Now.Date.AddDays(1).AddTicks(-1);

            // === 2. جلب الحركات داخل الفترة المحددة أولاً (باستخدام الـ Repositories) ===
            var invoicesInPeriod = await _context.PurchaseInvoices
                    .Include(i => i.PurchaseInvoiceItems).ThenInclude(item => item.Material) // <<< الخطوة 1: تضمين الأصناف
                    .Where(i => i.SupplierId == supplierId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate)
                    .ToListAsync();
            var paymentsInPeriod = await _context.SupplierPayments
                    .Where(p => p.SupplierId == supplierId && p.PaymentDate >= finalFromDate && p.PaymentDate <= finalToDate)
                    .ToListAsync();

            // --- 3. حساب صافي التغير خلال الفترة ---
            // بالنسبة للمورد، الفاتورة (Debit) تزيد مستحقاته، والدفعة (Credit) تقللها.
            decimal totalDebitInPeriod = invoicesInPeriod.Sum(i => i.TotalAmount);
            decimal totalCreditInPeriod = paymentsInPeriod.Sum(p => p.Amount);
            decimal netChangeInPeriod = totalDebitInPeriod - totalCreditInPeriod;

            // --- 4. حساب الرصيد الافتتاحي بالطريقة الصحيحة ---
            var supplier = await _context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId);
            if (supplier == null) return new List<AccountStatementViewModel>();

            // الرصيد الافتتاحي = الرصيد الإجمالي الحالي - صافي التغير الذي حدث داخل الفترة
            decimal openingBalance = supplier.Balance - netChangeInPeriod;

            // --- 5. دمج وفرز الحركات ---
            var allTransactions = new List<dynamic>();
            allTransactions.AddRange(invoicesInPeriod.Select(t => new {
                Date = t.InvoiceDate,
                Type = "فاتورة شراء",
                Ref = t.InvoiceNumber,
                DocId = (int?)t.Id,
                DocType = "PurchaseInvoice",
                Debit = t.TotalAmount,
                Credit = 0m,
                Items = t.PurchaseInvoiceItems
            }));

            allTransactions.AddRange(paymentsInPeriod.Select(t => new {
                Date = t.PaymentDate,
                Type = "دفعة لمورد",
                Ref = "دفعة #" + t.Id,
                DocId = t.PurchaseInvoiceId,
                DocType = "SupplierPayment",
                Debit = 0m,
                Credit = t.Amount,
                Items = new List<PurchaseInvoiceItem>()
            }));
            var sortedTransactions = allTransactions.OrderBy(t => t.Date).ThenBy(t => t.Ref).ToList();

            // --- 6. بناء التقرير النهائي ---
            var statement = new List<AccountStatementViewModel>();
            decimal currentBalance = openingBalance;

            // إضافة الرصيد المرحل كأول سجل مع التمييز بين مدين ودائن
            if (openingBalance > 0)
            {
                statement.Add(new AccountStatementViewModel { TransactionDate = statementDisplayDate, TransactionType = "رصيد افتتاحي (مدين)", Reference = "", Debit = openingBalance, Credit = 0m, Balance = openingBalance });
            }
            else if (openingBalance < 0)
            {
                statement.Add(new AccountStatementViewModel { TransactionDate = statementDisplayDate, TransactionType = "رصيد افتتاحي (دائن)", Reference = "", Debit = 0m, Credit = Math.Abs(openingBalance), Balance = openingBalance });
            }
            else
            {
                statement.Add(new AccountStatementViewModel { TransactionDate = statementDisplayDate, TransactionType = "رصيد افتتاحي (صفر)", Reference = "", Balance = 0 });
            }

            currentBalance = openingBalance; // إعادة تعيين الرصيد لبدء الحساب التراكمي

            foreach (var trans in sortedTransactions)
            {
                // معادلة المورد: الرصيد = الرصيد السابق + المشتريات (مدين) - المدفوعات (دائن)
                currentBalance = currentBalance + trans.Debit - trans.Credit;
                statement.Add(new AccountStatementViewModel
                {
                    TransactionDate = trans.Date,
                    TransactionType = trans.Type,
                    Reference = trans.Ref,
                    DocumentId = trans.DocId,
                    DocumentType = trans.DocType,
                    Debit = trans.Debit,
                    Credit = trans.Credit,
                    Balance = currentBalance,
                    Items = _mapper.Map<List<TransactionItemViewModel>>(trans.Items)
                });
            }

            // --- 7. تطبيق قاعدة "آخر 10 عمليات" ---
            if (!fromDate.HasValue && !toDate.HasValue && statement.Count > 11)
            {
                return statement.TakeLast(11).ToList();
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
                .Where(i => i.MaterialId == materialId && i.PurchaseInvoice.SupplierId.HasValue && i.PurchaseInvoice.InvoiceDate < finalFromDate)
                .SumAsync(i => (decimal?)i.Quantity) ?? 0;

            // مرتجعات البيع (تُضاف للمخزون) قبل الفترة
            var returnsBefore = await _context.PurchaseInvoiceItems
                .Where(i => i.MaterialId == materialId && i.PurchaseInvoice.ClientId.HasValue && i.PurchaseInvoice.InvoiceDate < finalFromDate)
                .SumAsync(i => (decimal?)i.Quantity) ?? 0;

            // المبيعات قبل الفترة
            var salesBefore = await _context.SalesInvoiceItems
                .Where(i => i.MaterialId == materialId && i.SalesInvoice.InvoiceDate < finalFromDate)
                .SumAsync(i => (decimal?)i.Quantity) ?? 0;

            decimal openingBalance = (purchasesBefore + returnsBefore) - salesBefore;

            // === 3. جلب الحركات خلال الفترة المحددة ===

            // المشتريات ومرتجعات البيع (وارد)
            var purchasesInPeriod = await _context.PurchaseInvoiceItems
                .Include(i => i.PurchaseInvoice.Supplier)
                .Include(i => i.PurchaseInvoice.Client)
                .Where(i => i.MaterialId == materialId && i.PurchaseInvoice.InvoiceDate >= finalFromDate && i.PurchaseInvoice.InvoiceDate <= finalToDate)
                .Select(i => new {
                    Date = i.PurchaseInvoice.InvoiceDate,
                    Type = i.PurchaseInvoice.ClientId.HasValue ? "مرتجع من عميل" : "شراء من مورد",
                    Ref = i.PurchaseInvoice.InvoiceNumber,
                    Qty = i.Quantity,
                    IsIn = true,
                    Source = i.PurchaseInvoice.ClientId.HasValue ? i.PurchaseInvoice.Client.Name : i.PurchaseInvoice.Supplier.Name
                }).ToListAsync();

            var salesInPeriod = await _context.SalesInvoiceItems
                .Include(i => i.SalesInvoice.Client)
                .Where(i => i.MaterialId == materialId && i.SalesInvoice.InvoiceDate >= finalFromDate && i.SalesInvoice.InvoiceDate <= finalToDate)
                .Select(i => new {
                    Date = i.SalesInvoice.InvoiceDate,
                    Type = "بيع لعميل",
                    Ref = i.SalesInvoice.InvoiceNumber,
                    Qty = i.Quantity,
                    IsIn = false,
                    Source = i.SalesInvoice.Client.Name
                }).ToListAsync();


            var allTransactions = purchasesInPeriod.Concat(salesInPeriod)
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
                .Include(i => i.Client)
                .Include(i => i.SalesInvoiceItems).ThenInclude(item => item.Material)
                .Where(i => i.InvoiceDate >= fromDate && i.InvoiceDate <= inclusiveToDate).ToListAsync();

            var report = new List<ProfitReportViewModel>();

            foreach (var invoice in salesInvoices)
            {
                decimal totalCost = invoice.SalesInvoiceItems.Sum(item => item.Quantity * (item.Material.PurchasePrice ?? 0));
                decimal profit = invoice.TotalAmount - totalCost;
                report.Add(new ProfitReportViewModel
                {
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    ClientName = invoice.Client.Name,
                    TotalAmount = invoice.TotalAmount,
                    TotalCost = totalCost,
                    Profit = profit
                });
            }
            return report.OrderBy(r => r.InvoiceDate).ToList();
        }


    }
}