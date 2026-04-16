using AutoMapper;
using MaterialManagement.BLL.ModelVM.Payment;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using MaterialManagement.DAL.Repo.Implementations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class SupplierPaymentService : ISupplierPaymentService
    {
        private readonly ISupplierPaymentRepo _supplierPaymentRepo; 
        private readonly MaterialManagementContext _context;
        private readonly IMapper _mapper;

        public SupplierPaymentService(
            ISupplierPaymentRepo supplierPaymentRepo,
            MaterialManagementContext context,
            IMapper mapper)
        {
            _supplierPaymentRepo = supplierPaymentRepo;
            _context = context;
            _mapper = mapper;
        }

        public async Task<SupplierPaymentViewModel> AddPaymentAsync(SupplierPaymentCreateModel model)
        {
            ValidateModel(model);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 2. جلب المورد للتتبع والتحديث
                // <<< الإصلاح الحاسم (2): استخدام _context مباشرة >>>
                var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == model.SupplierId);
                if (supplier == null) throw new InvalidOperationException("المورد غير موجود");

                // 3. تحديث الفاتورة (إذا كانت مرتبطة)
                if (model.PurchaseInvoiceId.HasValue)
                {
                    var invoice = await _context.PurchaseInvoices.FirstOrDefaultAsync(i => i.Id == model.PurchaseInvoiceId.Value);
                    if (invoice == null) throw new InvalidOperationException("فاتورة الشراء غير موجودة");

                    if (!invoice.SupplierId.HasValue || invoice.SupplierId.Value != model.SupplierId)
                        throw new InvalidOperationException("لا يمكن تسجيل دفعة على فاتورة لا تخص المورد المحدد.");

                    var remainingBeforePayment = CalculatePurchaseInvoiceRemaining(invoice);
                    if (remainingBeforePayment <= 0)
                        throw new InvalidOperationException("لا يوجد مبلغ متبقٍ على هذه الفاتورة.");

                    if (model.Amount > remainingBeforePayment)
                        throw new InvalidOperationException($"لا يمكن تسجيل دفعة أكبر من المتبقي على الفاتورة. المتبقي: {remainingBeforePayment:N2}.");

                    invoice.PaidAmount += model.Amount;
                    invoice.RemainingAmount = CalculatePurchaseInvoiceRemaining(invoice);
                }
                else
                {
                    if (supplier.Balance <= 0)
                        throw new InvalidOperationException("لا يوجد رصيد مستحق للمورد لتسجيل دفعة غير مرتبطة بفاتورة.");

                    if (model.Amount > supplier.Balance)
                        throw new InvalidOperationException($"لا يمكن تسجيل دفعة غير مرتبطة بفاتورة أكبر من رصيد المورد المستحق. الرصيد الحالي: {supplier.Balance:N2}.");
                }

                supplier.Balance -= model.Amount; // نقلل رصيد المورد (المبلغ الذي ندين به له)

                // 1. إنشاء كيان الدفع (في الذاكرة)
                var payment = _mapper.Map<SupplierPayment>(model);
                payment.PaymentDate = DateTime.Now;

                // <<< الإصلاح الحاسم (1): الإضافة مباشرة إلى الـ context >>>
                _context.SupplierPayments.Add(payment);

                // 4. حفظ كل التغييرات التي يتتبعها _context
                await _context.SaveChangesAsync();

                // 5. تأكيد الـ Transaction
                await transaction.CommitAsync();

                var viewModel = _mapper.Map<SupplierPaymentViewModel>(payment);
                viewModel.SupplierName = supplier.Name;
                return viewModel;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static void ValidateModel(SupplierPaymentCreateModel model)
        {
            if (model == null)
                throw new InvalidOperationException("بيانات دفعة المورد مطلوبة.");

            if (model.SupplierId <= 0)
                throw new InvalidOperationException("يجب اختيار المورد.");

            if (model.PurchaseInvoiceId.HasValue && model.PurchaseInvoiceId.Value <= 0)
                throw new InvalidOperationException("فاتورة الشراء المحددة غير صالحة.");

            if (model.Amount <= 0)
                throw new InvalidOperationException("المبلغ يجب أن يكون أكبر من الصفر.");
        }

        private static decimal CalculatePurchaseInvoiceRemaining(PurchaseInvoice invoice)
        {
            return invoice.TotalAmount - invoice.PaidAmount;
        }

        public async Task<IEnumerable<SupplierPaymentViewModel>> GetPaymentsForSupplierAsync(int supplierId)
        {

            var payments = await _supplierPaymentRepo.GetBySupplierIdAsync(supplierId);


            return _mapper.Map<IEnumerable<SupplierPaymentViewModel>>(payments);
        }

        public async Task<IEnumerable<SupplierPaymentViewModel>> GetPaymentsForInvoiceAsync(int invoiceId)
        {
            
            var payments = await _supplierPaymentRepo.GetByInvoiceIdAsync(invoiceId);

            return _mapper.Map<IEnumerable<SupplierPaymentViewModel>>(payments);
        }
    }
}
