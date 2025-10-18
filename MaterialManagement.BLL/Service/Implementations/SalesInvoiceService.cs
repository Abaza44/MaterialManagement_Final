using AutoMapper;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class SalesInvoiceService : ISalesInvoiceService
    {
        private readonly MaterialManagementContext _context;
        private readonly ISalesInvoiceRepo _invoiceRepo;
        private readonly IMaterialRepo _materialRepo;
        private readonly IMapper _mapper;

        // تم حذف _clientRepo لأنه لم نعد نستخدمه مباشرة هنا
        public SalesInvoiceService(
            MaterialManagementContext context,
            ISalesInvoiceRepo invoiceRepo,
            IMaterialRepo materialRepo,
            IMapper mapper)
        {
            _context = context;
            _invoiceRepo = invoiceRepo;
            _materialRepo = materialRepo;
            _mapper = mapper;

        }

        public async Task<SalesInvoiceViewModel> CreateInvoiceAsync(SalesInvoiceCreateModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. جلب العميل للتتبع
                var clientToUpdate = await _context.Clients.FindAsync(model.ClientId);
                if (clientToUpdate == null) throw new InvalidOperationException("العميل غير موجود");

                // 2. إنشاء رقم الفاتورة
                string finalInvoiceNumber;
                if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
                {
                    var allInvoiceNumbers = await _context.SalesInvoices
                        .Where(i => i.InvoiceNumber.StartsWith("SAL-"))
                        .Select(i => i.InvoiceNumber)
                        .ToListAsync();
                    var maxInvoiceNum = allInvoiceNumbers
                        .Select(numStr => int.TryParse(numStr.Substring(4), out int num) ? num : 0)
                        .DefaultIfEmpty(0).Max();
                    finalInvoiceNumber = $"SAL-{(maxInvoiceNum + 1):D5}";
                }
                else
                {
                    if (await _context.SalesInvoices.AnyAsync(i => i.InvoiceNumber == model.InvoiceNumber))
                        throw new InvalidOperationException($"رقم الفاتورة '{model.InvoiceNumber}' مستخدم بالفعل.");
                    finalInvoiceNumber = model.InvoiceNumber;
                }

                // 3. إنشاء الفاتورة
                var invoice = new SalesInvoice
                {
                    InvoiceNumber = finalInvoiceNumber,
                    InvoiceDate = model.InvoiceDate,
                    ClientId = model.ClientId,
                    PaidAmount = model.PaidAmount,
                    Notes = model.Notes,
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    SalesInvoiceItems = new List<SalesInvoiceItem>()
                };

                // 4. معالجة البنود وتحديث المخزون
                decimal totalAmount = 0;
                foreach (var item in model.Items)
                {
                    // استخدم _context.Materials.FindAsync لجلب المادة للتتبع
                    var material = await _context.Materials.FindAsync(item.MaterialId);
                    if (material == null) throw new InvalidOperationException($"المادة غير موجودة");
                    if (material.Quantity < item.Quantity) throw new InvalidOperationException($"الكمية غير كافية للمادة: '{material.Name}'.");

                    material.Quantity -= item.Quantity;
                    totalAmount += item.Quantity * item.UnitPrice;

                    invoice.SalesInvoiceItems.Add(new SalesInvoiceItem
                    {
                        MaterialId = item.MaterialId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.Quantity * item.UnitPrice
                    });
                }

                invoice.TotalAmount = totalAmount;
                invoice.RemainingAmount = totalAmount - model.PaidAmount;

                _context.SalesInvoices.Add(invoice);

                // 5. تحديث رصيد العميل
                clientToUpdate.Balance += invoice.RemainingAmount;

                // <<< الإصلاح الحاسم هنا >>>
                // أخبر EF صراحةً أن حالة هذا الكيان تغيرت
                _context.Entry(clientToUpdate).State = EntityState.Modified;

                // 6. حفظ كل التغييرات
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return _mapper.Map<SalesInvoiceViewModel>(invoice);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<SalesInvoiceViewModel>> GetAllInvoicesAsync()
        {
            var invoices = await _invoiceRepo.GetAllAsync();
            return _mapper.Map<IEnumerable<SalesInvoiceViewModel>>(invoices);
        }

        public async Task<SalesInvoiceViewModel?> GetInvoiceByIdAsync(int id)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id);
            return _mapper.Map<SalesInvoiceViewModel>(invoice);
        }

        public async Task DeleteInvoiceAsync(int id)
        {
            // ملاحظة: الحذف الآمن يجب أن يعيد الكميات إلى المخزون (عملية معقدة)
            await _invoiceRepo.DeleteAsync(id);
        }

        public async Task<IEnumerable<SalesInvoiceViewModel>> GetUnpaidInvoicesForClientAsync(int clientId)
        {
            var allInvoices = await _invoiceRepo.GetAllAsync();

            var unpaidInvoices = allInvoices
                .Where(i => i.ClientId == clientId && i.RemainingAmount > 0)
                .OrderByDescending(i => i.InvoiceDate);

            // <<< تم إضافة هذه الخطوة >>>
            // قم بتحويل قائمة الـ entities إلى قائمة الـ view models
            return _mapper.Map<IEnumerable<SalesInvoiceViewModel>>(unpaidInvoices);
        }

        public IQueryable<SalesInvoice> GetInvoicesAsQueryable()
        {
            return _invoiceRepo.GetAsQueryable();
        }

        public async Task<IEnumerable<ClientInvoiceSummaryViewModel>> GetClientInvoiceSummariesAsync()
        {

            var summariesDto = await _invoiceRepo.GetClientInvoiceSummariesAsync();

            return _mapper.Map<IEnumerable<ClientInvoiceSummaryViewModel>>(summariesDto);
        }
    }
}