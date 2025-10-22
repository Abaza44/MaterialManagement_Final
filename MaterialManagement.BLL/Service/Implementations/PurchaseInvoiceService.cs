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
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Use AutoMapper for initial mapping (including DiscountAmount if mapped)
                var invoice = _mapper.Map<PurchaseInvoice>(model);
                invoice.InvoiceNumber = $"PUR-{DateTime.Now.Ticks}"; // Simplified
                invoice.InvoiceDate = DateTime.Now; // Ensure invoice date is set
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
                    if (model.SupplierId.HasValue) // Purchase
                    {
                        material.Quantity += itemModel.Quantity;
                        material.PurchasePrice = itemModel.UnitPrice;
                    }
                    else if (model.ClientId.HasValue) // Return from Client
                    {
                        material.Quantity += itemModel.Quantity;
                        // Don't update purchase price on returns
                    }
                }

                // --- Apply Discount Logic ---

                // 1. Set Gross Total and Discount Amount
                invoice.TotalAmount = grossTotalAmount - invoice.DiscountAmount;
                // Ensure DiscountAmount is correctly mapped or set (AutoMapper might handle this)
                // If DiscountAmount is not mapped automatically, uncomment the next line:
                // invoice.DiscountAmount = model.DiscountAmount;

                // 2. Calculate Net Amount Due (after discount)


                // 3. Calculate Remaining Amount (Net Amount - Paid Amount)
                invoice.RemainingAmount = invoice.TotalAmount - model.PaidAmount;
                invoice.PaidAmount = model.PaidAmount; // Make sure PaidAmount is also saved

                // --- End Discount Logic ---

                await _invoiceRepo.AddAsync(invoice);

                // --- Update Balances ---
                if (model.SupplierId.HasValue) // Purchase from Supplier
                {
                    var supplier = await _supplierRepo.GetByIdForUpdateAsync(model.SupplierId.Value);
                    if (supplier == null) throw new InvalidOperationException("المورد غير موجود");

                    // Our debt to the supplier increases by the remaining amount
                    supplier.Balance += invoice.RemainingAmount;
                }
                else if (model.ClientId.HasValue) // Return from Client
                {
                    var client = await _clientRepo.GetByIdForUpdateAsync(model.ClientId.Value);
                    if (client == null) throw new InvalidOperationException("العميل غير موجود");

                    // Client's debt to us decreases by the net value of the returned goods
                    // (RemainingAmount here represents how much we still owe the client for the return if PaidAmount < netAmountDue)
                    client.Balance -= invoice.TotalAmount;

                    // Adjust balance further based on payment during return
                    // If we paid the client during the return (model.PaidAmount > 0), their debt decreases even more.
                    // If the client paid us during the return (e.g., restocking fee, model.PaidAmount < 0?), debt increases.
                    // Note: Typically for returns, RemainingAmount = netAmountDue - PaidAmount.
                    // client.Balance adjustment could also be seen as:
                    // client.Balance -= (netAmountDue - model.PaidAmount); // Decrease debt by amount we owe them
                    // client.Balance -= invoice.RemainingAmount; // Equivalent if RemainingAmount calculated correctly
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

                // 3. عكس التأثير المالي
                if (invoiceToDelete.SupplierId.HasValue)
                {
                    invoiceToDelete.Supplier.Balance -= invoiceToDelete.RemainingAmount;
                }
                else if (invoiceToDelete.ClientId.HasValue)
                {
                    invoiceToDelete.Client.Balance += invoiceToDelete.TotalAmount;
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