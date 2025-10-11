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
                if (!model.SupplierId.HasValue && !model.ClientId.HasValue)
                    throw new InvalidOperationException("يجب تحديد مورد أو عميل.");
                if (model.SupplierId.HasValue && model.ClientId.HasValue)
                    throw new InvalidOperationException("لا يمكن تحديد مورد وعميل في نفس الوقت.");

                string finalInvoiceNumber;
                if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
                {
                    // --- <<< تم إصلاح الأخطاء الإملائية هنا >>> ---
                    var allInvoiceNumberParts = await _context.PurchaseInvoices
                                              .Where(i => i.InvoiceNumber.StartsWith("PUR-"))
                                              .Select(i => i.InvoiceNumber.Substring(4))
                                              .ToListAsync();

                    // 2. الآن، قم بمعالجة الأرقام في الذاكرة باستخدام LINQ to Objects
                    var maxInvoiceNum = allInvoiceNumberParts
                        .Select(numStr => int.TryParse(numStr, out int num) ? num : 0)
                        .DefaultIfEmpty(0)
                        .Max();

                    finalInvoiceNumber = $"PUR-{(maxInvoiceNum + 1):D5}";
                }
                else
                {
                    if (await _context.PurchaseInvoices.AnyAsync(i => i.InvoiceNumber == model.InvoiceNumber))
                        throw new InvalidOperationException($"رقم الفاتورة '{model.InvoiceNumber}' مستخدم بالفعل.");
                    finalInvoiceNumber = model.InvoiceNumber;
                }

                var invoice = new PurchaseInvoice
                {
                    InvoiceNumber = finalInvoiceNumber,
                    InvoiceDate = model.InvoiceDate,
                    SupplierId = model.SupplierId,
                    ClientId = model.ClientId,
                    PaidAmount = model.PaidAmount,
                    Notes = model.Notes,
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    PurchaseInvoiceItems = new List<PurchaseInvoiceItem>()
                };

                decimal totalAmount = 0;
                foreach (var itemModel in model.Items)
                {
                    var material = await _context.Materials.FindAsync(itemModel.MaterialId);
                    if (material == null) throw new InvalidOperationException("المادة غير موجودة");

                    material.Quantity += itemModel.Quantity;
                    if (model.SupplierId.HasValue)
                    {
                        material.PurchasePrice = itemModel.UnitPrice;
                    }
                    totalAmount += itemModel.Quantity * itemModel.UnitPrice;

                    var invoiceItem = _mapper.Map<PurchaseInvoiceItem>(itemModel);
                    invoiceItem.TotalPrice = itemModel.Quantity * itemModel.UnitPrice;
                    invoice.PurchaseInvoiceItems.Add(invoiceItem);
                }

                invoice.TotalAmount = totalAmount;
                invoice.RemainingAmount = totalAmount - model.PaidAmount;
                _context.PurchaseInvoices.Add(invoice);

                if (model.SupplierId.HasValue)
                {
                    var supplierToUpdate = await _context.Suppliers.FindAsync(model.SupplierId.Value);
                    if (supplierToUpdate == null) throw new InvalidOperationException("المورد المحدد غير موجود");

                    supplierToUpdate.Balance += invoice.RemainingAmount;
                    _context.Entry(supplierToUpdate).State = EntityState.Modified;
                }
                else if (model.ClientId.HasValue)
                {
                    var clientToUpdate = await _context.Clients.FindAsync(model.ClientId.Value);
                    if (clientToUpdate == null) throw new InvalidOperationException("العميل المحدد غير موجود");

                    clientToUpdate.Balance -= invoice.TotalAmount;
                    _context.Entry(clientToUpdate).State = EntityState.Modified;
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