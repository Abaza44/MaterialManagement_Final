using AutoMapper;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB; // <-- مهم جدًا
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class PurchaseInvoiceService : IPurchaseInvoiceService
    {
        private readonly MaterialManagementContext _context;
        private readonly IPurchaseInvoiceRepo _invoiceRepo;
        private readonly IMaterialRepo _materialRepo;
        private readonly IMapper _mapper;

        public PurchaseInvoiceService(
            MaterialManagementContext context,
            IPurchaseInvoiceRepo invoiceRepo,
            IMaterialRepo materialRepo,
            IMapper mapper)
        {
            _context = context;
            _invoiceRepo = invoiceRepo;
            _materialRepo = materialRepo;
            _mapper = mapper;
        }

        public async Task<PurchaseInvoiceViewModel> CreateInvoiceAsync(PurchaseInvoiceCreateModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var supplierToUpdate = await _context.Suppliers.FindAsync(model.SupplierId);
                if (supplierToUpdate == null) throw new InvalidOperationException("المورد غير موجود");

                // --- <<< تم استبدال هذا الجزء بالكامل بالمنطق الجديد >>> ---
                if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
                {
                    var allInvoiceNumbers = await _context.PurchaseInvoices
                                                  .Where(i => i.InvoiceNumber.StartsWith("PUR-"))
                                                  .Select(i => i.InvoiceNumber)
                                                  .ToListAsync();

                    var maxInvoiceNum = allInvoiceNumbers
                        .Select(numStr => int.TryParse(numStr.Substring(4), out int num) ? num : 0)
                        .DefaultIfEmpty(0)
                        .Max();

                    model.InvoiceNumber = $"PUR-{(maxInvoiceNum + 1):D5}";
                }
                else
                {
                    if (await _context.PurchaseInvoices.AnyAsync(i => i.InvoiceNumber == model.InvoiceNumber))
                    {
                        throw new InvalidOperationException($"رقم الفاتورة '{model.InvoiceNumber}' مستخدم بالفعل.");
                    }
                }
                // --- نهاية الجزء المستبدل ---

                var invoice = _mapper.Map<PurchaseInvoice>(model);
                invoice.CreatedDate = DateTime.Now;

                decimal totalAmount = 0;
                foreach (var item in model.Items)
                {
                    var material = await _materialRepo.GetByIdForUpdateAsync(item.MaterialId);
                    if (material == null) throw new InvalidOperationException($"المادة بكود {item.MaterialId} غير موجودة");

                    material.Quantity += item.Quantity;
                    material.PurchasePrice = item.UnitPrice;
                    totalAmount += item.Quantity * item.UnitPrice;
                }

                invoice.TotalAmount = totalAmount;
                invoice.PaidAmount = model.PaidAmount;
                invoice.RemainingAmount = totalAmount - model.PaidAmount;

                invoice.PurchaseInvoiceItems = model.Items.Select(item => _mapper.Map<PurchaseInvoiceItem>(item)).ToList();

                _context.PurchaseInvoices.Add(invoice);
                supplierToUpdate.Balance += invoice.RemainingAmount;

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

        // --- باقي الدوال ---

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

        public async Task DeleteInvoiceAsync(int id)
        {
            await _invoiceRepo.DeleteAsync(id);
        }

        public async Task<IEnumerable<PurchaseInvoiceViewModel>> GetUnpaidInvoicesForSupplierAsync(int supplierId)
        {
            var allInvoices = await _invoiceRepo.GetAllAsync();
            var unpaidInvoices = allInvoices
                .Where(i => i.SupplierId == supplierId && i.RemainingAmount > 0)
                .OrderByDescending(i => i.InvoiceDate);

            return _mapper.Map<IEnumerable<PurchaseInvoiceViewModel>>(unpaidInvoices);
        }
    }
}