using AutoMapper;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                // ... (Invoice number generation logic can be moved to the Repo for cleanliness)
                var invoice = _mapper.Map<PurchaseInvoice>(model);
                invoice.InvoiceNumber = $"PUR-{DateTime.Now.Ticks}"; // Simplified for now

                decimal totalAmount = 0;
                foreach (var itemModel in model.Items)
                {
                    var material = await _materialRepo.GetByIdForUpdateAsync(itemModel.MaterialId);
                    if (material == null) throw new InvalidOperationException("المادة غير موجودة");

                    material.Quantity += itemModel.Quantity;
                    if (model.SupplierId.HasValue) { material.PurchasePrice = itemModel.UnitPrice; }

                    totalAmount += itemModel.Quantity * itemModel.UnitPrice;
                    invoice.PurchaseInvoiceItems.Add(_mapper.Map<PurchaseInvoiceItem>(itemModel));
                }

                invoice.TotalAmount = totalAmount;
                invoice.RemainingAmount = totalAmount - model.PaidAmount;

                await _invoiceRepo.AddAsync(invoice);

                // Update Supplier/Client Balance using Repositories
                if (model.SupplierId.HasValue)
                {
                    var supplier = await _supplierRepo.GetByIdForUpdateAsync(model.SupplierId.Value);
                    if (supplier == null) throw new InvalidOperationException("المورد غير موجود");
                    supplier.Balance += invoice.RemainingAmount;
                }
                else if (model.ClientId.HasValue)
                {
                    var client = await _clientRepo.GetByIdForUpdateAsync(model.ClientId.Value);
                    if (client == null) throw new InvalidOperationException("العميل غير موجود");
                    client.Balance -= invoice.TotalAmount;
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