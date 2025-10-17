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
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. إنشاء كيان الدفع (في الذاكرة)
                var payment = _mapper.Map<SupplierPayment>(model);
                payment.PaymentDate = DateTime.Now;

                // <<< الإصلاح الحاسم (1): الإضافة مباشرة إلى الـ context >>>
                _context.SupplierPayments.Add(payment);

                // 2. جلب المورد للتتبع والتحديث
                // <<< الإصلاح الحاسم (2): استخدام _context مباشرة >>>
                var supplier = await _context.Suppliers.FindAsync(model.SupplierId);
                if (supplier == null) throw new InvalidOperationException("Supplier not found");

                supplier.Balance -= model.Amount; // نقلل رصيد المورد (المبلغ الذي ندين به له)

                // 3. تحديث الفاتورة (إذا كانت مرتبطة)
                if (model.PurchaseInvoiceId.HasValue)
                {
                    var invoice = await _context.PurchaseInvoices.FindAsync(model.PurchaseInvoiceId.Value);
                    if (invoice == null) throw new InvalidOperationException("Invoice not found");

                    invoice.PaidAmount += model.Amount;
                    invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
                }

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

        public async Task<IEnumerable<SupplierPaymentViewModel>> GetPaymentsForSupplierAsync(int supplierId)
        {

            var payments = await _supplierPaymentRepo.GetBySupplierIdAsync(supplierId);


            return _mapper.Map<IEnumerable<SupplierPaymentViewModel>>(payments);
        }
    }
}