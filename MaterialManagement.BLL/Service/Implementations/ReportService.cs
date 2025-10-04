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
            // <<< الإصلاح هنا: تعديل toDate ليشمل اليوم بأكمله >>>
            var inclusiveToDate = toDate.Date.AddDays(1).AddTicks(-1);

            // --- الخطوة 1: حساب الرصيد الافتتاحي الدقيق ---
            var totalInvoicesBefore = await _context.SalesInvoices
                .Where(i => i.ClientId == clientId && i.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

            var totalPaymentsBefore = await _context.ClientPayments
                .Where(p => p.ClientId == clientId && p.PaymentDate < fromDate)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            decimal openingBalance = totalInvoicesBefore - totalPaymentsBefore;

            // --- الخطوة 2: جلب كل الحركات خلال الفترة ---
            var invoicesInPeriod = await _context.SalesInvoices
                // استخدم inclusiveToDate هنا
                .Where(i => i.ClientId == clientId && i.InvoiceDate >= fromDate && i.InvoiceDate <= inclusiveToDate)
                .Select(i => new { Date = i.InvoiceDate, Type = "فاتورة بيع", Ref = i.InvoiceNumber, Debit = i.TotalAmount, Credit = 0m })
                .ToListAsync();

            var paymentsInPeriod = await _context.ClientPayments
                // واستخدمه هنا أيضًا
                .Where(p => p.ClientId == clientId && p.PaymentDate >= fromDate && p.PaymentDate <= inclusiveToDate)
                .Select(p => new { Date = p.PaymentDate, Type = "تحصيل", Ref = "دفعة #" + p.Id, Debit = 0m, Credit = p.Amount })
                .ToListAsync();

            // ... باقي الكود يبقى كما هو ...
            var allTransactions = invoicesInPeriod.Concat(paymentsInPeriod)
                                                  .OrderBy(t => t.Date)
                                                  .ToList();

            var statement = new List<AccountStatementViewModel>();
            decimal currentBalance = openingBalance;

            statement.Add(new AccountStatementViewModel
            {
                TransactionDate = fromDate,
                TransactionType = "رصيد مرحل",
                Balance = currentBalance
            });

            foreach (var trans in allTransactions)
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

        // (منطق الموردين سيتم إضافته لاحقًا بنفس الطريقة)
        public async Task<List<AccountStatementViewModel>> GetSupplierAccountStatementAsync(int supplierId, DateTime fromDate, DateTime toDate)
        {
            var inclusiveToDate = toDate.Date.AddDays(1).AddTicks(-1);

            // --- الخطوة 1: حساب الرصيد الافتتاحي الدقيق ---
            var totalInvoicesBefore = await _context.PurchaseInvoices
                .Where(i => i.SupplierId == supplierId && i.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

            var totalPaymentsBefore = await _context.SupplierPayments
                .Where(p => p.SupplierId == supplierId && p.PaymentDate < fromDate)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            decimal openingBalance = totalInvoicesBefore - totalPaymentsBefore;

            // --- الخطوة 2: جلب كل الحركات خلال الفترة ---
            var invoicesInPeriod = await _context.PurchaseInvoices
                .Where(i => i.SupplierId == supplierId && i.InvoiceDate >= fromDate && i.InvoiceDate <= inclusiveToDate)
                .Select(i => new { Date = i.InvoiceDate, Type = "فاتورة شراء", Ref = i.InvoiceNumber, Debit = i.TotalAmount, Credit = 0m })
                .ToListAsync();

            var paymentsInPeriod = await _context.SupplierPayments
                .Where(p => p.SupplierId == supplierId && p.PaymentDate >= fromDate && p.PaymentDate <= inclusiveToDate)
                .Select(p => new { Date = p.PaymentDate, Type = "دفعة لمورد", Ref = "دفعة #" + p.Id, Debit = 0m, Credit = p.Amount })
                .ToListAsync();

            // --- الخطوة 3: دمج وترتيب الحركات ---
            var allTransactions = invoicesInPeriod
                .Select(t => new { t.Date, t.Type, t.Ref, t.Debit, t.Credit })
                .Concat(paymentsInPeriod.Select(t => new { t.Date, t.Type, t.Ref, t.Debit, t.Credit }))
                .OrderBy(t => t.Date)
                .ToList();

            // --- الخطوة 4: بناء كشف الحساب النهائي ---
            var statement = new List<AccountStatementViewModel>();
            decimal currentBalance = openingBalance;

            // إضافة الرصيد الافتتاحي
            statement.Add(new AccountStatementViewModel
            {
                TransactionDate = fromDate,
                TransactionType = "رصيد مرحل",
                Debit = 0,
                Credit = 0,
                Balance = currentBalance
            });

            foreach (var trans in allTransactions)
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

            // 1. حساب الرصيد الافتتاحي (كمية المادة قبل fromDate)
            var purchasesBefore = await _context.PurchaseInvoiceItems
                .Where(i => i.MaterialId == materialId && i.PurchaseInvoice.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.Quantity) ?? 0;

            var salesBefore = await _context.SalesInvoiceItems
                .Where(i => i.MaterialId == materialId && i.SalesInvoice.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.Quantity) ?? 0;

            decimal openingBalance = purchasesBefore - salesBefore;

            // 2. جلب الحركات خلال الفترة
            var purchasesInPeriod = await _context.PurchaseInvoiceItems
                .Where(i => i.MaterialId == materialId && i.PurchaseInvoice.InvoiceDate >= fromDate && i.PurchaseInvoice.InvoiceDate <= inclusiveToDate)
                .Select(i => new { Date = i.PurchaseInvoice.InvoiceDate, Type = "شراء", Ref = i.PurchaseInvoice.InvoiceNumber, Qty = i.Quantity, IsIn = true })
                .ToListAsync();

            var salesInPeriod = await _context.SalesInvoiceItems
                .Where(i => i.MaterialId == materialId && i.SalesInvoice.InvoiceDate >= fromDate && i.SalesInvoice.InvoiceDate <= inclusiveToDate)
                .Select(i => new { Date = i.SalesInvoice.InvoiceDate, Type = "بيع", Ref = i.SalesInvoice.InvoiceNumber, Qty = i.Quantity, IsIn = false })
                .ToListAsync();

            // 3. دمج وترتيب الحركات
            var allTransactions = purchasesInPeriod.Concat(salesInPeriod)
                                                   .OrderBy(t => t.Date)
                                                   .ToList();

            // 4. بناء التقرير النهائي
            var report = new List<MaterialMovementViewModel>();
            decimal currentBalance = openingBalance;

            report.Add(new MaterialMovementViewModel
            {
                TransactionDate = fromDate,
                TransactionType = "رصيد افتتاحي",
                Balance = currentBalance
            });

            foreach (var trans in allTransactions)
            {
                if (trans.IsIn) // حركة شراء (وارد)
                {
                    currentBalance += trans.Qty;
                    report.Add(new MaterialMovementViewModel
                    {
                        TransactionDate = trans.Date,
                        TransactionType = trans.Type,
                        InvoiceNumber = trans.Ref,
                        QuantityIn = trans.Qty,
                        QuantityOut = 0,
                        Balance = currentBalance
                    });
                }
                else // حركة بيع (صادر)
                {
                    currentBalance -= trans.Qty;
                    report.Add(new MaterialMovementViewModel
                    {
                        TransactionDate = trans.Date,
                        TransactionType = trans.Type,
                        InvoiceNumber = trans.Ref,
                        QuantityIn = 0,
                        QuantityOut = trans.Qty,
                        Balance = currentBalance
                    });
                }
            }

            return report;
        }

        public async Task<List<ProfitReportViewModel>> GetProfitReportAsync(DateTime fromDate, DateTime toDate)
        {
            var inclusiveToDate = toDate.Date.AddDays(1).AddTicks(-1);

            // 1. جلب كل فواتير البيع خلال الفترة مع كل تفاصيلها
            var salesInvoices = await _context.SalesInvoices
                .Include(i => i.Client)
                .Include(i => i.SalesInvoiceItems)
                    .ThenInclude(item => item.Material) // مهم جدًا لجلب سعر الشراء
                .Where(i => i.InvoiceDate >= fromDate && i.InvoiceDate <= inclusiveToDate)
                .ToListAsync();

            var report = new List<ProfitReportViewModel>();

            // 2. المرور على كل فاتورة لحساب تكلفتها وربحها
            foreach (var invoice in salesInvoices)
            {
                // حساب التكلفة الإجمالية للفاتورة
                decimal totalCost = 0;
                foreach (var item in invoice.SalesInvoiceItems)
                {
                    // تكلفة البند = الكمية المباعة * سعر الشراء للمادة
                    // (نستخدم ?? 0 للتعامل مع حالة أن سعر الشراء قد يكون null)
                    totalCost += item.Quantity * (item.Material.PurchasePrice ?? 0);
                }

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