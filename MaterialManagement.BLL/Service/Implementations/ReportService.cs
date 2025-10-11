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

        public async Task<List<AccountStatementViewModel>> GetClientAccountStatementAsync(int clientId, DateTime fromDate, DateTime toDate)
        {
            var inclusiveToDate = toDate.Date.AddDays(1).AddTicks(-1);

            // --- 1. حساب الرصيد الافتتاحي (مع المرتجعات) ---
            decimal totalSalesBefore = 0;
            decimal totalReturnsBefore = 0;
            decimal paymentsOnInvoicesBefore = 0;
            decimal separatePaymentsBefore = 0;

            totalSalesBefore = await _context.SalesInvoices
                .Where(i => i.ClientId == clientId && i.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

            totalReturnsBefore = await _context.PurchaseInvoices
                .Where(i => i.ClientId == clientId && i.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

            paymentsOnInvoicesBefore = await _context.SalesInvoices
                .Where(i => i.ClientId == clientId && i.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.PaidAmount) ?? 0;

            separatePaymentsBefore = await _context.ClientPayments
                .Where(p => p.ClientId == clientId && p.PaymentDate < fromDate)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            decimal openingBalance = totalSalesBefore - (totalReturnsBefore + paymentsOnInvoicesBefore + separatePaymentsBefore);

            // --- 2. جلب كل أنواع الحركات خلال الفترة ---
            var salesInPeriod = await _context.SalesInvoices
                .Where(i => i.ClientId == clientId && i.InvoiceDate >= fromDate && i.InvoiceDate <= inclusiveToDate)
                .Select(i => new { Date = i.InvoiceDate, Type = "فاتورة بيع", Ref = i.InvoiceNumber, Debit = i.TotalAmount, Credit = i.PaidAmount })
                .ToListAsync();

            var paymentsInPeriod = await _context.ClientPayments
                .Where(p => p.ClientId == clientId && p.PaymentDate >= fromDate && p.PaymentDate <= inclusiveToDate)
                .Select(p => new { Date = p.PaymentDate, Type = "تحصيل", Ref = "دفعة #" + p.Id, Debit = 0m, Credit = p.Amount })
                .ToListAsync();

            var returnsInPeriod = await _context.PurchaseInvoices
                .Where(i => i.ClientId == clientId && i.InvoiceDate >= fromDate && i.InvoiceDate <= inclusiveToDate)
                .Select(i => new { Date = i.InvoiceDate, Type = "مرتجع بيع", Ref = i.InvoiceNumber, Debit = 0m, Credit = i.TotalAmount })
                .ToListAsync();

            // --- 3. دمج كل الحركات ---
            var allTransactions = new List<dynamic>();

            // الفواتير كـ "مدين"
            allTransactions.AddRange(salesInPeriod.Select(t => new { t.Date, t.Type, t.Ref, Debit = t.Debit, Credit = 0m }));
            // الدفعات مع الفواتير كـ "دائن"
            allTransactions.AddRange(salesInPeriod.Where(t => t.Credit > 0).Select(t => new { t.Date, Type = "دفعة مع الفاتورة", Ref = t.Ref, Debit = 0m, Credit = t.Credit }));
            // التحصيلات المستقلة كـ "دائن"
            allTransactions.AddRange(paymentsInPeriod.Select(t => new { t.Date, t.Type, t.Ref, t.Debit, t.Credit }));
            // المرتجعات كـ "دائن"
            allTransactions.AddRange(returnsInPeriod.Select(t => new { t.Date, t.Type, t.Ref, t.Debit, t.Credit }));

            var sortedTransactions = allTransactions.OrderBy(t => t.Date).ToList();

            // --- 4. بناء التقرير النهائي ---
            var statement = new List<AccountStatementViewModel>();
            decimal currentBalance = openingBalance;

            statement.Add(new AccountStatementViewModel { TransactionDate = fromDate, TransactionType = "رصيد مرحل", Balance = currentBalance });

            foreach (var trans in sortedTransactions)
            {
                currentBalance = currentBalance + trans.Debit - trans.Credit;
                statement.Add(new AccountStatementViewModel
                {
                    TransactionDate = trans.Date,
                    TransactionType = trans.Type,
                    Reference = trans.Ref,
                    Debit = trans.Debit,
                    Credit = trans.Credit,
                    Balance = currentBalance
                });
            }

            return statement;
        }

        public async Task<List<AccountStatementViewModel>> GetSupplierAccountStatementAsync(int supplierId, DateTime fromDate, DateTime toDate)
        {
            var inclusiveToDate = toDate.Date.AddDays(1).AddTicks(-1);

            // 1. حساب الرصيد الافتتاحي الدقيق للمورد
            var totalInvoicesBefore = await _context.PurchaseInvoices
                .Where(i => i.SupplierId == supplierId && i.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

            var paymentsOnInvoicesBefore = await _context.PurchaseInvoices
                .Where(i => i.SupplierId == supplierId && i.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.PaidAmount) ?? 0;

            var separatePaymentsBefore = await _context.SupplierPayments
                .Where(p => p.SupplierId == supplierId && p.PaymentDate < fromDate)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            decimal openingBalance = totalInvoicesBefore - (paymentsOnInvoicesBefore + separatePaymentsBefore);

            // 2. جلب الحركات خلال الفترة
            var invoicesInPeriod = await _context.PurchaseInvoices
                .Where(i => i.SupplierId == supplierId && i.InvoiceDate >= fromDate && i.InvoiceDate <= inclusiveToDate)
                .Select(i => new { Date = i.InvoiceDate, Type = "فاتورة شراء", Ref = i.InvoiceNumber, Debit = i.TotalAmount, Credit = i.PaidAmount })
                .ToListAsync();

            var paymentsInPeriod = await _context.SupplierPayments
                .Where(p => p.SupplierId == supplierId && p.PaymentDate >= fromDate && p.PaymentDate <= inclusiveToDate)
                .Select(p => new { Date = p.PaymentDate, Type = "دفعة لمورد", Ref = "دفعة #" + p.Id, Debit = 0m, Credit = p.Amount })
                .ToListAsync();

            // 3. دمج الحركات
            var allTransactions = new List<dynamic>();
            allTransactions.AddRange(invoicesInPeriod.Select(t => new { t.Date, t.Type, t.Ref, Debit = t.Debit, Credit = 0m }));
            allTransactions.AddRange(invoicesInPeriod.Where(t => t.Credit > 0).Select(t => new { t.Date, Type = "دفعة مع الفاتورة", Ref = t.Ref, Debit = 0m, Credit = t.Credit }));
            allTransactions.AddRange(paymentsInPeriod.Select(t => new { t.Date, t.Type, t.Ref, t.Debit, t.Credit }));

            var sortedTransactions = allTransactions.OrderBy(t => t.Date).ToList();

            // 4. بناء التقرير
            var statement = new List<AccountStatementViewModel>();
            decimal currentBalance = openingBalance;

            statement.Add(new AccountStatementViewModel { TransactionDate = fromDate, TransactionType = "رصيد مرحل", Balance = currentBalance });

            foreach (var trans in sortedTransactions)
            {
                currentBalance = currentBalance + trans.Debit - trans.Credit;
                statement.Add(new AccountStatementViewModel
                {
                    TransactionDate = trans.Date,
                    TransactionType = trans.Type,
                    Reference = trans.Ref,
                    Debit = trans.Debit,
                    Credit = trans.Credit,
                    Balance = currentBalance
                });
            }

            return statement;
        }

        public async Task<List<MaterialMovementViewModel>> GetMaterialMovementAsync(int materialId, DateTime fromDate, DateTime toDate)
        {
            var inclusiveToDate = toDate.Date.AddDays(1).AddTicks(-1);

            // --- 1. حساب الرصيد الافتتاحي (مع المرتجعات) ---
            var purchasesBefore = await _context.PurchaseInvoiceItems
                .Where(i => i.MaterialId == materialId && i.PurchaseInvoice.SupplierId.HasValue && i.PurchaseInvoice.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.Quantity) ?? 0;

            var returnsBefore = await _context.PurchaseInvoiceItems
                .Where(i => i.MaterialId == materialId && i.PurchaseInvoice.ClientId.HasValue && i.PurchaseInvoice.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.Quantity) ?? 0;

            var salesBefore = await _context.SalesInvoiceItems
                .Where(i => i.MaterialId == materialId && i.SalesInvoice.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.Quantity) ?? 0;

            decimal openingBalance = (purchasesBefore + returnsBefore) - salesBefore;

            // --- 2. جلب الحركات خلال الفترة مع المصدر ---
            var purchasesInPeriod = await _context.PurchaseInvoiceItems
                .Include(i => i.PurchaseInvoice.Supplier)
                .Include(i => i.PurchaseInvoice.Client)
                .Where(i => i.MaterialId == materialId && i.PurchaseInvoice.InvoiceDate >= fromDate && i.PurchaseInvoice.InvoiceDate <= inclusiveToDate)
                .Select(i => new {
                    Date = i.PurchaseInvoice.InvoiceDate,
                    Type = i.PurchaseInvoice.ClientId.HasValue ? "مرتجع من عميل" : "شراء من مورد",
                    Ref = i.PurchaseInvoice.InvoiceNumber,
                    Qty = i.Quantity,
                    IsIn = true,
                    Source = i.PurchaseInvoice.ClientId.HasValue ? i.PurchaseInvoice.Client.Name : i.PurchaseInvoice.Supplier.Name
                }).ToListAsync();

            var salesInPeriod = await _context.SalesInvoiceItems
                .Include(i => i.SalesInvoice.Client) // <<< تم إصلاح المشكلة هنا
                .Where(i => i.MaterialId == materialId && i.SalesInvoice.InvoiceDate >= fromDate && i.SalesInvoice.InvoiceDate <= inclusiveToDate)
                .Select(i => new {
                    Date = i.SalesInvoice.InvoiceDate,
                    Type = "بيع لعميل",
                    Ref = i.SalesInvoice.InvoiceNumber,
                    Qty = i.Quantity,
                    IsIn = false,
                    Source = i.SalesInvoice.Client.Name // <<< الآن هذا سيعمل
                }).ToListAsync();

            // 3. دمج وترتيب الحركات
            var allTransactions = purchasesInPeriod.Concat(salesInPeriod)
                                                   .OrderBy(t => t.Date)
                                                   .ToList();

            // 4. بناء التقرير النهائي
            var report = new List<MaterialMovementViewModel>();
            decimal currentBalance = openingBalance;

            report.Add(new MaterialMovementViewModel { TransactionDate = fromDate, TransactionType = "رصيد افتتاحي", Balance = currentBalance, Source = "" });

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
            return report;
        }

        public async Task<List<ProfitReportViewModel>> GetProfitReportAsync(DateTime fromDate, DateTime toDate)
        {
            // (هذا الكود صحيح من المرة السابقة)
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