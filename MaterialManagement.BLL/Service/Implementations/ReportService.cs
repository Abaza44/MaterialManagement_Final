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
        public ReportService(MaterialManagementContext context,IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<List<AccountStatementViewModel>> GetClientAccountStatementAsync(int clientId, DateTime? fromDate, DateTime? toDate)
        {
            var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return new List<AccountStatementViewModel>();

            DateTime finalFromDate = fromDate ?? DateTime.MinValue;
            DateTime finalToDate = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Now.Date.AddDays(1).AddTicks(-1);
            DateTime statementDisplayDate = fromDate ?? DateTime.Today;

            // === 2. جلب الحركات (الكيانات الكاملة) ===
            var salesInPeriod = await _context.SalesInvoices
                .Include(i => i.SalesInvoiceItems).ThenInclude(item => item.Material)
                .Where(i => i.ClientId == clientId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate && i.IsActive)
                .ToListAsync(); // <-- (تعديل 1: جلب الكيان كاملاً)

            var paymentsInPeriod = await _context.ClientPayments
                .Where(p => p.ClientId == clientId && p.PaymentDate >= finalFromDate && p.PaymentDate <= finalToDate)
                .ToListAsync(); // <-- (تعديل 1: جلب الكيان كاملاً)

            var returnsInPeriod = await _context.PurchaseInvoices
                .Include(i => i.PurchaseInvoiceItems).ThenInclude(item => item.Material)
                .Where(i => i.ClientId == clientId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate && i.IsActive)
                .ToListAsync(); // <-- (تعديل 1: جلب الكيان كاملاً)

            // === 3. حساب صافي التغير (باستخدام اللوجيك الجديد) ===

            // (تعديل 2: حساب المدين = صافي الفاتورة بعد الخصم)
            decimal totalDebitInPeriod = salesInPeriod.Sum(i => i.TotalAmount - i.DiscountAmount);

            // (تعديل 3: حساب الدائن = كل المدفوعات + المرتجعات + الدفع عند الاستلام)
            decimal totalCreditInPeriod =
                paymentsInPeriod.Sum(p => p.Amount) +
                returnsInPeriod.Sum(i => i.TotalAmount) +
                salesInPeriod.Sum(i => i.PaidAmount); // <-- (هذا هو الجزء المفقود)

            decimal netChangeInPeriod = totalDebitInPeriod - totalCreditInPeriod;
            decimal openingBalance = client.Balance - netChangeInPeriod;

            // === 4. بناء قائمة الحركات المفصلة ===
            var allTransactions = new List<dynamic>();

            // إضافة المبيعات (كحركتين منفصلتين)
            // إضافة المبيعات (كحركة واحدة مدمجة)
            foreach (var invoice in salesInPeriod)
            {
                allTransactions.Add(new
                {
                    Date = invoice.InvoiceDate,
                    Type = "فاتورة بيع",
                    Ref = invoice.InvoiceNumber,
                    DocId = (int?)invoice.Id,
                    DocType = "SalesInvoice",
                    Debit = invoice.TotalAmount, 
                    Credit = invoice.PaidAmount, 
                    Items = invoice.SalesInvoiceItems
                });
            }

            // إضافة التحصيلات العادية
            allTransactions.AddRange(paymentsInPeriod.Select(p => new {
                Date = p.PaymentDate,
                Type = "تحصيل",
                Ref = "دفعة #" + p.Id,
                DocId = p.SalesInvoiceId,
                DocType = "ClientPayment",
                Debit = 0m,
                Credit = p.Amount,
                Items = new List<SalesInvoiceItem>() // الدفعة ليس لها أصناف
            }));

            // إضافة المرتجعات
            allTransactions.AddRange(returnsInPeriod.Select(i => new {
                Date = i.InvoiceDate,
                Type = "مرتجع بيع",
                Ref = i.InvoiceNumber,
                DocId = (int?)i.Id,
                DocType = "PurchaseInvoice",
                Debit = 0m,
                Credit = i.TotalAmount,
                Items = i.PurchaseInvoiceItems
            }));

            var sortedTransactions = allTransactions.OrderBy(t => t.Date).ThenBy(t => t.Ref).ToList();

            // === 5. بناء كشف الحساب النهائي ===
            var statement = new List<AccountStatementViewModel>();
            decimal currentBalance = openingBalance;

            // ... (جزء الرصيد الافتتاحي سليم كما هو) ...
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
                    Items = trans.Items != null
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
            // (بافتراض أنك أضفت حقلي DiscountAmount و PaidAmount لـ PurchaseInvoice أيضاً)

            DateTime finalFromDate = fromDate ?? DateTime.MinValue;
            DateTime statementDisplayDate = fromDate ?? DateTime.Today;
            DateTime finalToDate = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Now.Date.AddDays(1).AddTicks(-1);

            var invoicesInPeriod = await _context.PurchaseInvoices
                .Include(i => i.PurchaseInvoiceItems).ThenInclude(item => item.Material)
                .Where(i => i.SupplierId == supplierId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate && i.IsActive)
                .ToListAsync(); // <-- (جلب الكيان كاملاً)

            var paymentsInPeriod = await _context.SupplierPayments
                .Where(p => p.SupplierId == supplierId && p.PaymentDate >= finalFromDate && p.PaymentDate <= finalToDate)
                .ToListAsync(); // <-- (جلب الكيان كاملاً)

            // --- 3. حساب صافي التغير ---
            // (المدين = صافي فاتورة الشراء بعد الخصم)
            decimal totalDebitInPeriod = invoicesInPeriod.Sum(i => i.TotalAmount - i.DiscountAmount); // (بافتراض وجود الخصم)

            // (الدائن = المدفوعات + الدفع عند الاستلام)
            decimal totalCreditInPeriod =
                paymentsInPeriod.Sum(p => p.Amount) +
                invoicesInPeriod.Sum(i => i.PaidAmount); // (بافتراض وجود دفع عند الاستلام)

            decimal netChangeInPeriod = totalDebitInPeriod - totalCreditInPeriod;

            var supplier = await _context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId);
            if (supplier == null) return new List<AccountStatementViewModel>();

            decimal openingBalance = supplier.Balance - netChangeInPeriod;

            // --- 5. دمج وفرز الحركات ---
            var allTransactions = new List<dynamic>();

            // إضافة فواتير الشراء (كحركتين)
            // إضافة فواتير الشراء (كحركة واحدة مدمجة)
            foreach (var invoice in invoicesInPeriod)
            {
                allTransactions.Add(new
                {
                    Date = invoice.InvoiceDate,
                    Type = "فاتورة شراء",
                    Ref = invoice.InvoiceNumber,
                    DocId = (int?)invoice.Id,
                    DocType = "PurchaseInvoice",
                    Debit = invoice.TotalAmount, // (صافي المديونية للمورد)
                    Credit = invoice.PaidAmount, // (المدفوع للمورد في نفس الحركة)
                    Items = invoice.PurchaseInvoiceItems
                });
            }

            // إضافة المدفوعات العادية
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

            // ... (جزء الرصيد الافتتاحي سليم كما هو) ...
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
                    Items = _mapper.Map<List<TransactionItemViewModel>>(trans.Items)
                });
            }

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