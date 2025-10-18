using MaterialManagement.BLL.ModelVM.Reports;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
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

        public ReportService(MaterialManagementContext context)
        {
            _context = context;
        }

        public async Task<List<AccountStatementViewModel>> GetClientAccountStatementAsync(int clientId, DateTime? fromDate, DateTime? toDate)
        {
            // === 1. تحديد نطاق البحث ===
            // إذا لم يتم تحديد تاريخ بداية، سنستخدم أقدم تاريخ ممكن منطقياً
            DateTime finalFromDate = fromDate ?? DateTime.MinValue;
            // إذا لم يتم تحديد تاريخ نهاية، نستخدم تاريخ اليوم (ونشمل اليوم بأكمله)
            DateTime finalToDate = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Now.Date.AddDays(1).AddTicks(-1);

            // الرصيد الافتتاحي يُحسب دائماً حتى تاريخ البداية (finalFromDate)

            // --- 1. حساب الرصيد الافتتاحي (مدين - دائن) ---
            // المصادر: [مبيعات] - [مرتجعات + تحصيلات]

            // إجمالي المبيعات قبل الفترة
            decimal totalSalesBefore = await _context.SalesInvoices
                .Where(i => i.ClientId == clientId && i.InvoiceDate < finalFromDate)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

            // إجمالي مرتجعات البيع (التي تُعتبر خصماً على العميل) قبل الفترة
            decimal totalReturnsBefore = await _context.PurchaseInvoices
                .Where(i => i.ClientId == clientId && i.InvoiceDate < finalFromDate)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

            // إجمالي المدفوعات المسجلة على الفواتير قبل الفترة
            decimal paymentsOnInvoicesBefore = await _context.SalesInvoices
                .Where(i => i.ClientId == clientId && i.InvoiceDate < finalFromDate)
                .SumAsync(i => (decimal?)i.PaidAmount) ?? 0;

            // إجمالي التحصيلات المستقلة قبل الفترة
            decimal separatePaymentsBefore = await _context.ClientPayments
                .Where(p => p.ClientId == clientId && p.PaymentDate < finalFromDate)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            decimal paymentsBefore = paymentsOnInvoicesBefore + separatePaymentsBefore;
            decimal openingBalance = totalSalesBefore - (totalReturnsBefore + paymentsBefore);


            // --- 2. جلب كل أنواع الحركات خلال الفترة المحددة (finalFromDate حتى finalToDate) ---

            var salesInPeriod = await _context.SalesInvoices
                    .Where(i => i.ClientId == clientId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate)
                    .Select(i => new
                    {
                        Date = i.InvoiceDate,
                        Type = "فاتورة بيع",
                        Ref = i.InvoiceNumber,
                        DocId = (int?)i.Id, 
                        DocType = "SalesInvoice", 
                        Debit = i.TotalAmount,
                        Credit = i.PaidAmount
                    })
                    .ToListAsync();
            var paymentsInPeriod = await _context.ClientPayments
                    .Where(p => p.ClientId == clientId && p.PaymentDate >= finalFromDate && p.PaymentDate <= finalToDate)
                    .Select(p => new
                    {
                        Date = p.PaymentDate,
                        Type = "تحصيل",
                        Ref = "دفعة #" + p.Id,
                        DocId = p.SalesInvoiceId, 
                        DocType = "ClientPayment",
                        Debit = 0m,
                        Credit = p.Amount
                    })
                    .ToListAsync();

            var returnsInPeriod = await _context.PurchaseInvoices
                    .Where(i => i.ClientId == clientId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate)
                    .Select(i => new
                    {
                        Date = i.InvoiceDate,
                        Type = "مرتجع بيع",
                        Ref = i.InvoiceNumber,
                        DocId = (int?)i.Id,
                        DocType = "PurchaseInvoice",
                        Debit = 0m,
                        Credit = i.TotalAmount
                    })
                    .ToListAsync();

            var allTransactions = new List<dynamic>();

            // 1. المبيعات (تزيد المديونية - مدين)
            allTransactions.AddRange(salesInPeriod.Select(t => new { t.Date, t.Type, t.Ref, t.DocId, t.DocType, Debit = t.Debit, Credit = 0m }));

            // 2. التحصيلات (تقلل المديونية - دائن)
            // 2. التحصيلات والمرتجعات (تقلل المديونية - دائن)
            allTransactions.AddRange(paymentsInPeriod.Select(t => new { t.Date, t.Type, t.Ref, t.DocId, t.DocType, Debit = 0m, Credit = t.Credit }));
            allTransactions.AddRange(returnsInPeriod.Select(t => new { t.Date, t.Type, t.Ref, t.DocId, t.DocType, Debit = 0m, Credit = t.Credit }));


            var sortedTransactions = allTransactions
                    .OrderBy(t => t.Date)
                    .ThenBy(t => t.Ref)
                    .ToList();

            // --- 4. بناء التقرير النهائي وحساب الرصيد التراكمي ---
            var statement = new List<AccountStatementViewModel>();
            decimal currentBalance = openingBalance;

            // إضافة الرصيد المرحل كأول سجل
            statement.Add(new AccountStatementViewModel
            {
                TransactionDate = finalFromDate,
                TransactionType = "رصيد مرحل",
                Balance = currentBalance,
                Reference = ""
            });

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
                    Balance = currentBalance
                });
            }

            if (!fromDate.HasValue && !toDate.HasValue)
            {
                return statement.TakeLast(11).ToList();
            }

            return statement;
        }



        public async Task<List<AccountStatementViewModel>> GetSupplierAccountStatementAsync(int supplierId, DateTime? fromDate, DateTime? toDate)
        {
            // === 1. تحديد نطاق البحث ===
            DateTime finalFromDate = fromDate ?? DateTime.MinValue;
            DateTime finalToDate = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : DateTime.Now.Date.AddDays(1).AddTicks(-1);

            // --- 1. حساب الرصيد الافتتاحي (مدين - دائن) ---
            // رصيد المورد: [مشتريات] - [مدفوعات] (إذا كان موجبًا فهو مستحق للمورد)

            var totalInvoicesBefore = await _context.PurchaseInvoices
                .Where(i => i.SupplierId == supplierId && i.InvoiceDate < finalFromDate)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

            var paymentsBefore = await _context.SupplierPayments
                .Where(p => p.SupplierId == supplierId && p.PaymentDate < finalFromDate)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            decimal openingBalance = totalInvoicesBefore - paymentsBefore;

            var invoicesInPeriod = await _context.PurchaseInvoices
        .Where(i => i.SupplierId == supplierId && i.InvoiceDate >= finalFromDate && i.InvoiceDate <= finalToDate)
        .Select(i => new
        {
            Date = i.InvoiceDate,
            Type = "فاتورة شراء",
            Ref = i.InvoiceNumber,
            DocId = (int?)i.Id,
            DocType = "PurchaseInvoice",
            Debit = i.TotalAmount,
            Credit = 0m
        })
        .ToListAsync();

            var paymentsInPeriod = await _context.SupplierPayments
                .Where(p => p.SupplierId == supplierId && p.PaymentDate >= finalFromDate && p.PaymentDate <= finalToDate)
                .Select(p => new
                {
                    Date = p.PaymentDate,
                    Type = "دفعة لمورد",
                    Ref = "دفعة #" + p.Id,
                    DocId = p.PurchaseInvoiceId,
                    DocType = "SupplierPayment",
                    Debit = 0m,
                    Credit = p.Amount
                })
                .ToListAsync();

            // --- 3. دمج الحركات ---
            var allTransactions = new List<dynamic>();

            // المشتريات (تزيد المستحقات - مدين)
            allTransactions.AddRange(invoicesInPeriod.Select(t => new { t.Date, t.Type, t.Ref, t.DocId, t.DocType, Debit = t.Debit, Credit = 0m }));
            // المدفوعات (تقلل المستحقات - دائن)
            allTransactions.AddRange(paymentsInPeriod.Select(t => new { t.Date, t.Type, t.Ref, t.DocId, t.DocType, Debit = 0m, Credit = t.Credit }));

            var sortedTransactions = allTransactions
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Ref)
                .ToList();

            // --- 4. بناء التقرير النهائي وحساب الرصيد التراكمي ---
            var statement = new List<AccountStatementViewModel>();
            decimal currentBalance = openingBalance;

            // إضافة الرصيد المرحل كأول سجل
            statement.Add(new AccountStatementViewModel
            {
                TransactionDate = finalFromDate,
                TransactionType = "رصيد مرحل",
                Balance = currentBalance,
                Reference = ""
            });

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
                    Balance = currentBalance
                });
            }


            if (!fromDate.HasValue && !toDate.HasValue)
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