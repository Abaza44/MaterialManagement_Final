using AutoMapper;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using MaterialManagement.DAL.Repo.Implementations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class SalesInvoiceService : ISalesInvoiceService
    {
        private readonly ISalesInvoiceRepo _invoiceRepo;
        private readonly IMaterialRepo _materialRepo;
        private readonly IClientRepo _clientRepo; // <<< نحتاجه لتعديل رصيد العميل
        private readonly MaterialManagementContext _context; // <<< نحتاجه لإدارة الـ Transaction
        private readonly IMapper _mapper;

        public SalesInvoiceService(
            ISalesInvoiceRepo invoiceRepo,
            IMaterialRepo materialRepo,
            IClientRepo clientRepo, // <<< تم إضافته
            MaterialManagementContext context,
            IMapper mapper)
        {
            _invoiceRepo = invoiceRepo;
            _materialRepo = materialRepo;
            _clientRepo = clientRepo; // <<< تم إضافته
            _context = context;
            _mapper = mapper;
        }

        public async Task<SalesInvoiceViewModel> CreateInvoiceAsync(SalesInvoiceCreateModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var clientToUpdate = await _clientRepo.GetByIdForUpdateAsync(model.ClientId);
                if (clientToUpdate == null) throw new InvalidOperationException("العميل غير موجود");

                // (تعديل 1: بناء الفاتورة يدوياً لضمان الدقة بدلاً من المابر)
                var invoice = new SalesInvoice
                {
                    ClientId = model.ClientId,
                    InvoiceDate = DateTime.Now,
                    InvoiceNumber = $"SAL-{DateTime.Now.Ticks}",
                    // Notes = model.Notes // (إذا كان لديك حقل ملاحظات)
                };

                decimal totalAmount = 0;
                foreach (var item in model.Items)
                {
                    var material = await _materialRepo.GetByIdForUpdateAsync(item.MaterialId);
                    if (material == null) throw new InvalidOperationException($"المادة غير موجودة");
                    if (material.Quantity < item.Quantity) throw new InvalidOperationException($"الكمية غير كافية للمادة: '{material.Name}'.");

                    material.Quantity -= item.Quantity;

                    var itemTotal = item.Quantity * item.UnitPrice;
                    totalAmount += itemTotal;

                    var invoiceItem = _mapper.Map<SalesInvoiceItem>(item);
                    invoiceItem.TotalPrice = itemTotal; // (تأكد من حفظ إجمالي البند أيضاً)
                    invoice.SalesInvoiceItems.Add(invoiceItem);
                }

                // ▼▼▼ (التعديل 2: تطبيق لوجيك الخصم الجديد) ▼▼▼

                // 1. الإجمالي الفعلي (قيمة البضاعة)
                invoice.TotalAmount = totalAmount; // مثال: 17965

                // 2. تسجيل المدفوع والخصم (من الفورم)
                invoice.PaidAmount = model.PaidAmount;         // مثال: 17900
                invoice.DiscountAmount = model.DiscountAmount; // مثال: 65

                // 3. حساب الصافي المستحق (المبلغ المطلوب من العميل بعد الخصم)
                decimal netAmountDue = invoice.TotalAmount - invoice.DiscountAmount; // 17965 - 65 = 17900

                // 4. حساب المتبقي على الفاتورة (المطلوب - المدفوع)
                invoice.RemainingAmount = netAmountDue - invoice.PaidAmount; // 17900 - 17900 = 0

                // 5. إضافة الفاتورة للـ DB Context
                await _invoiceRepo.AddAsync(invoice);

                // 6. تحديث رصيد العميل (المديونية)
                // المديونية تزيد فقط بالمبلغ المتبقي (اللي هو 0 في مثالك)
                clientToUpdate.Balance += invoice.RemainingAmount;

                await _context.SaveChangesAsync(); // حفظ كل التغييرات (الفاتورة، العميل، المواد)
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
            var invoice = await _invoiceRepo.GetByIdWithDetailsAsync(id);
            return _mapper.Map<SalesInvoiceViewModel>(invoice);
        }

        public async Task DeleteInvoiceAsync(int id)
        {
            // 1. نبدأ معاملة لضمان تنفيذ كل شيء أو لا شيء
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 2. جلب الفاتورة مع كل الأصناف والعميل (للتحديث)
                var invoiceToDelete = await _invoiceRepo.GetByIdForUpdateAsync(id);
                if (invoiceToDelete == null)
                    throw new InvalidOperationException("الفاتورة غير موجودة");

                invoiceToDelete.Client.Balance -= invoiceToDelete.RemainingAmount;


                foreach (var item in invoiceToDelete.SalesInvoiceItems)
                {
                    item.Material.Quantity += item.Quantity;
                }

                // 5. تنفيذ الحذف الناعم (Soft Delete)
                _invoiceRepo.Delete(invoiceToDelete);

                // 6. حفظ كل التغييرات مرة واحدة
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
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