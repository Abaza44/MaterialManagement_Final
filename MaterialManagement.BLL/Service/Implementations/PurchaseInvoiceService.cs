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
using MaterialManagement.BLL.ModelVM.Supplier;
using MaterialManagement.DAL.DTOs;
using MaterialManagement.DAL.Enums;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class PurchaseInvoiceService : IPurchaseInvoiceService
    {
        private readonly IPurchaseInvoiceRepo _invoiceRepo;
        private readonly IMaterialRepo _materialRepo;
        private readonly ISupplierRepo _supplierRepo;
        private readonly IClientRepo _clientRepo;
        private readonly MaterialManagementContext _context; 
        private readonly IMapper _mapper;

        public PurchaseInvoiceService(
            IPurchaseInvoiceRepo invoiceRepo,
            IMaterialRepo materialRepo,
            ISupplierRepo supplierRepo,
            IClientRepo clientRepo,
            MaterialManagementContext context,
            IMapper mapper)
        {
            _invoiceRepo = invoiceRepo;
            _materialRepo = materialRepo;
            _supplierRepo = supplierRepo;
            _clientRepo = clientRepo;
            _context = context;
            _mapper = mapper;
        }

        public async Task<PurchaseInvoiceViewModel> CreateInvoiceAsync(PurchaseInvoiceCreateModel model)
        {
            ValidateCreateModel(model);
            var mode = ResolveCreateMode(model);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Use AutoMapper for initial mapping (including DiscountAmount if mapped)
                var invoice = _mapper.Map<PurchaseInvoice>(model);
                invoice.InvoiceNumber = $"PUR-{DateTime.Now.Ticks}"; // Simplified
                invoice.InvoiceDate = DateTime.Now; // Ensure invoice date is set
                ApplyModeToInvoice(invoice, model, mode);
                decimal grossTotalAmount = 0;
                
                foreach (var itemModel in model.Items)
                {
                    var material = await _materialRepo.GetByIdForUpdateAsync(itemModel.MaterialId);
                    if (material == null) throw new InvalidOperationException("المادة غير موجودة");

                    var itemTotal = itemModel.Quantity * itemModel.UnitPrice;
                    grossTotalAmount += itemTotal;

                    // Map the item and ensure TotalPrice is set
                    var invoiceItem = _mapper.Map<PurchaseInvoiceItem>(itemModel);
                    invoiceItem.TotalPrice = itemTotal;
                    invoice.PurchaseInvoiceItems.Add(invoiceItem);

                    // Update Stock and Purchase Price based on transaction type
                    if (mode == PurchaseInvoiceBusinessMode.SupplierPurchase || mode == PurchaseInvoiceBusinessMode.OneTimeSupplier)
                    {
                        material.Quantity += itemModel.Quantity;
                        material.PurchasePrice = itemModel.UnitPrice;
                    }
                    else if (mode == PurchaseInvoiceBusinessMode.ClientReturn)
                    {
                        material.Quantity += itemModel.Quantity;
                        // Don't update purchase price on returns
                    }
                }

                // --- Apply Discount Logic ---

                // 1. Set Gross Total and Discount Amount
                invoice.DiscountAmount = model.DiscountAmount;
                ValidateTotals(grossTotalAmount, invoice.DiscountAmount, model.PaidAmount);
                invoice.TotalAmount = grossTotalAmount - invoice.DiscountAmount;

                // 2. Calculate Net Amount Due (after discount)


                // 3. Calculate Remaining Amount (Net Amount - Paid Amount)
                invoice.RemainingAmount = invoice.TotalAmount - model.PaidAmount;
                invoice.PaidAmount = model.PaidAmount; // Make sure PaidAmount is also saved

                if (mode == PurchaseInvoiceBusinessMode.OneTimeSupplier && invoice.RemainingAmount != 0)
                {
                    throw new InvalidOperationException("عملية المورد اليدوي يجب أن تكون مسددة بالكامل. إذا كان هناك متبقي، سجّل المورد أولاً.");
                }

                // --- End Discount Logic ---

                await _invoiceRepo.AddAsync(invoice);

                // --- Update Balances ---
                if (mode == PurchaseInvoiceBusinessMode.SupplierPurchase)
                {
                    var supplierId = invoice.SupplierId
                        ?? throw new InvalidOperationException("المورد المحدد غير صالح.");
                    var supplier = await _supplierRepo.GetByIdForUpdateAsync(supplierId);
                    if (supplier == null) throw new InvalidOperationException("المورد غير موجود");

                    // Our debt to the supplier increases by the remaining amount
                    supplier.Balance += invoice.RemainingAmount;
                }
                else if (mode == PurchaseInvoiceBusinessMode.ClientReturn)
                {
                    var clientId = invoice.ClientId
                        ?? throw new InvalidOperationException("العميل المحدد غير صالح.");
                    var client = await _clientRepo.GetByIdForUpdateAsync(clientId);
                    if (client == null) throw new InvalidOperationException("العميل غير موجود");

                    client.Balance -= invoice.RemainingAmount;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return _mapper.Map<PurchaseInvoiceViewModel>(invoice);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // --- هذا هو الحل لمشكلة الحذف ---
        public async Task DeleteInvoiceAsync(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var invoiceToDelete = await _invoiceRepo.GetByIdForUpdateAsync(id);
                if (invoiceToDelete == null)
                    throw new InvalidOperationException("الفاتورة غير موجودة");

                var mode = ResolvePersistedMode(invoiceToDelete);
                ValidateDeleteStockReversal(invoiceToDelete);

                // 3. عكس التأثير المالي
                if (mode == PurchaseInvoiceBusinessMode.SupplierPurchase)
                {
                    if (invoiceToDelete.Supplier == null)
                        throw new InvalidOperationException("المورد المرتبط بهذه العملية غير موجود.");

                    invoiceToDelete.Supplier.Balance -= invoiceToDelete.RemainingAmount;
                }
                else if (mode == PurchaseInvoiceBusinessMode.ClientReturn)
                {
                    if (invoiceToDelete.Client == null)
                        throw new InvalidOperationException("العميل المرتبط بهذه العملية غير موجود.");

                    invoiceToDelete.Client.Balance += invoiceToDelete.RemainingAmount;
                }

                // 4. عكس التأثير المخزني
                foreach (var item in invoiceToDelete.PurchaseInvoiceItems)
                {
                    item.Material.Quantity -= item.Quantity;
                }

                // 5. تنفيذ الحذف الناعم
                _invoiceRepo.Delete(invoiceToDelete);

                // 6. حفظ كل التغييرات
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<PurchaseInvoiceViewModel>> GetAllInvoicesAsync()
        {
            var invoices = await _invoiceRepo.GetAllAsync();
            return _mapper.Map<IEnumerable<PurchaseInvoiceViewModel>>(invoices);
        }

        private static void ValidateCreateModel(PurchaseInvoiceCreateModel model)
        {
            if (model == null)
                throw new InvalidOperationException("بيانات فاتورة الشراء مطلوبة.");

            if (model.Items == null || !model.Items.Any())
                throw new InvalidOperationException("يجب إضافة بند واحد على الأقل.");

            if (model.DiscountAmount < 0)
                throw new InvalidOperationException("مبلغ الخصم لا يمكن أن يكون سالباً.");

            if (model.PaidAmount < 0)
                throw new InvalidOperationException("المبلغ المسدد لا يمكن أن يكون سالباً.");

            foreach (var item in model.Items)
            {
                if (item.MaterialId <= 0)
                    throw new InvalidOperationException("يجب اختيار مادة صحيحة لكل بند.");

                if (item.Quantity <= 0)
                    throw new InvalidOperationException("الكمية يجب أن تكون أكبر من الصفر.");

                if (item.UnitPrice <= 0)
                    throw new InvalidOperationException("سعر الوحدة يجب أن يكون أكبر من الصفر.");
            }
        }

        private static void ValidateTotals(decimal grossTotalAmount, decimal discountAmount, decimal paidAmount)
        {
            if (discountAmount > grossTotalAmount)
                throw new InvalidOperationException("مبلغ الخصم لا يمكن أن يكون أكبر من إجمالي الفاتورة.");

            var netAmountDue = grossTotalAmount - discountAmount;
            if (paidAmount > netAmountDue)
                throw new InvalidOperationException($"المبلغ المسدد لا يمكن أن يكون أكبر من صافي الفاتورة. الصافي: {netAmountDue:N2}.");
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static PurchaseInvoiceBusinessMode ResolveCreateMode(PurchaseInvoiceCreateModel model)
        {
            if (model.PartyMode == PurchaseInvoicePartyMode.RegisteredSupplier)
            {
                if (!model.SupplierId.HasValue || model.SupplierId.Value <= 0)
                    throw new InvalidOperationException("يجب اختيار مورد مسجل لعملية الشراء.");

                if (model.ClientId.HasValue)
                    throw new InvalidOperationException("لا يمكن اختيار عميل في عملية شراء من مورد.");

                return PurchaseInvoiceBusinessMode.SupplierPurchase;
            }

            if (model.PartyMode == PurchaseInvoicePartyMode.OneTimeSupplier)
            {
                if (model.SupplierId.HasValue || model.ClientId.HasValue)
                    throw new InvalidOperationException("المورد اليدوي لا يستخدم رقم مورد أو عميل مسجل.");

                if (string.IsNullOrWhiteSpace(model.OneTimeSupplierName))
                    throw new InvalidOperationException("يجب إدخال اسم المورد اليدوي.");

                return PurchaseInvoiceBusinessMode.OneTimeSupplier;
            }

            if (model.PartyMode == PurchaseInvoicePartyMode.RegisteredClientReturn)
            {
                if (!model.ClientId.HasValue || model.ClientId.Value <= 0)
                    throw new InvalidOperationException("يجب اختيار عميل مسجل لعملية المرتجع.");

                if (model.SupplierId.HasValue)
                    throw new InvalidOperationException("لا يمكن اختيار مورد في عملية مرتجع من عميل.");

                return PurchaseInvoiceBusinessMode.ClientReturn;
            }

            throw new InvalidOperationException("نوع العملية غير صالح.");
        }

        private static PurchaseInvoiceBusinessMode ResolvePersistedMode(PurchaseInvoice invoice)
        {
            if (invoice.PartyMode == PurchaseInvoicePartyMode.RegisteredSupplier)
            {
                if (!invoice.SupplierId.HasValue || invoice.ClientId.HasValue)
                    throw new InvalidOperationException("لا يمكن عكس هذه العملية لأن بيانات المورد غير متسقة. يرجى مراجعة السجل يدوياً.");

                return PurchaseInvoiceBusinessMode.SupplierPurchase;
            }

            if (invoice.PartyMode == PurchaseInvoicePartyMode.OneTimeSupplier)
            {
                if (invoice.SupplierId.HasValue || invoice.ClientId.HasValue)
                    throw new InvalidOperationException("لا يمكن عكس هذه العملية لأن بيانات المورد اليدوي غير متسقة. يرجى مراجعة السجل يدوياً.");

                return PurchaseInvoiceBusinessMode.OneTimeSupplier;
            }

            if (invoice.PartyMode == PurchaseInvoicePartyMode.RegisteredClientReturn)
            {
                if (!invoice.ClientId.HasValue || invoice.SupplierId.HasValue)
                    throw new InvalidOperationException("لا يمكن عكس هذه العملية لأن بيانات العميل غير متسقة. يرجى مراجعة السجل يدوياً.");

                return PurchaseInvoiceBusinessMode.ClientReturn;
            }

            throw new InvalidOperationException("لا يمكن عكس هذه العملية لأن نوعها غير صالح.");
        }

        private static void ApplyModeToInvoice(
            PurchaseInvoice invoice,
            PurchaseInvoiceCreateModel model,
            PurchaseInvoiceBusinessMode mode)
        {
            if (mode == PurchaseInvoiceBusinessMode.SupplierPurchase)
            {
                invoice.PartyMode = PurchaseInvoicePartyMode.RegisteredSupplier;
                invoice.SupplierId = model.SupplierId!.Value;
                invoice.ClientId = null;
                invoice.OneTimeSupplierName = null;
                invoice.OneTimeSupplierPhone = null;
            }
            else if (mode == PurchaseInvoiceBusinessMode.OneTimeSupplier)
            {
                invoice.PartyMode = PurchaseInvoicePartyMode.OneTimeSupplier;
                invoice.SupplierId = null;
                invoice.ClientId = null;
                invoice.OneTimeSupplierName = model.OneTimeSupplierName?.Trim();
                invoice.OneTimeSupplierPhone = NormalizeOptionalText(model.OneTimeSupplierPhone);
            }
            else
            {
                invoice.PartyMode = PurchaseInvoicePartyMode.RegisteredClientReturn;
                invoice.SupplierId = null;
                invoice.ClientId = model.ClientId!.Value;
                invoice.OneTimeSupplierName = null;
                invoice.OneTimeSupplierPhone = null;
            }
        }

        private static void ValidateDeleteStockReversal(PurchaseInvoice invoice)
        {
            foreach (var item in invoice.PurchaseInvoiceItems)
            {
                if (item.Material == null)
                    throw new InvalidOperationException("لا يمكن عكس هذه العملية لأن المادة المرتبطة بأحد البنود غير موجودة.");

                var quantityAfterReversal = item.Material.Quantity - item.Quantity;
                if (quantityAfterReversal < 0)
                    throw new InvalidOperationException($"لا يمكن حذف العملية لأن حذفها سيجعل رصيد المادة سالباً: {item.Material.Name}.");

                if (quantityAfterReversal < item.Material.ReservedQuantity)
                    throw new InvalidOperationException($"لا يمكن حذف العملية لأن الكمية المتبقية من المادة محجوزة حالياً: {item.Material.Name}.");
            }
        }

        private enum PurchaseInvoiceBusinessMode
        {
            SupplierPurchase,
            OneTimeSupplier,
            ClientReturn
        }

        public async Task<PurchaseInvoiceViewModel?> GetInvoiceByIdAsync(int id)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id);
            return _mapper.Map<PurchaseInvoiceViewModel>(invoice);
        }

        public async Task<IEnumerable<PurchaseInvoiceViewModel>> GetUnpaidInvoicesForSupplierAsync(int supplierId)
        {
            // (هذا يجب أن يستخدم دالة Repository مخصصة، لكن سنتركه الآن للتبسيط)
            var allInvoices = await _invoiceRepo.GetAllAsync();
            var unpaidInvoices = allInvoices
                .Where(i => i.SupplierId == supplierId && i.RemainingAmount > 0)
                .OrderByDescending(i => i.InvoiceDate);
            return _mapper.Map<IEnumerable<PurchaseInvoiceViewModel>>(unpaidInvoices);
        }

        public IQueryable<PurchaseInvoice> GetInvoicesAsQueryable()
        {
            return _invoiceRepo.GetAsQueryable();
        }

        public async Task<IEnumerable<SupplierInvoiceSummaryViewModel>> GetSupplierInvoiceSummariesAsync()
        {
            var summariesDto = await _invoiceRepo.GetSupplierInvoiceSummariesAsync();
            return _mapper.Map<IEnumerable<SupplierInvoiceSummaryViewModel>>(summariesDto);
        }
    }
}
