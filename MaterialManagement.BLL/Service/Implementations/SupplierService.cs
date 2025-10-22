using AutoMapper;
using MaterialManagement.BLL.ModelVM.Supplier;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using MaterialManagement.DAL.Repo.Implementations;
using Microsoft.EntityFrameworkCore;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class SupplierService : ISupplierService
    {
        private readonly ISupplierRepo _supplierRepo;
        private readonly IMapper _mapper;
        private readonly MaterialManagementContext _context;

        public SupplierService(ISupplierRepo supplierRepo, IMapper mapper, MaterialManagementContext context)
        {
            _supplierRepo = supplierRepo;
            _mapper = mapper;
            _context = context;

        }

        public async Task<IEnumerable<SupplierViewModel>> GetAllSuppliersAsync()
        {
            var suppliers = await _supplierRepo.GetAllAsync();
            return _mapper.Map<IEnumerable<SupplierViewModel>>(suppliers);
        }

        public async Task<SupplierViewModel?> GetSupplierByIdAsync(int id)
        {
            var supplier = await _supplierRepo.GetByIdAsync(id);
            return supplier != null ? _mapper.Map<SupplierViewModel>(supplier) : null;
        }

        public async Task<SupplierViewModel> CreateSupplierAsync(SupplierCreateModel model)
        {
            
            if (!string.IsNullOrEmpty(model.Phone) && await _supplierRepo.PhoneExistsAsync(model.Phone))
            {
                throw new InvalidOperationException("❌ يوجد مورد مسجل بنفس رقم الهاتف بالفعل");
            }
            var allSuppliers = await _supplierRepo.GetAllAsync();
            if (!string.IsNullOrEmpty(model.Phone) && allSuppliers.Any(s => s.Phone == model.Phone))
            {
                throw new InvalidOperationException("❌ يوجد مورد مسجل بنفس رقم الهاتف بالفعل");
            }

            var supplier = _mapper.Map<Supplier>(model);
            supplier.CreatedDate = DateTime.Now;

            var createdSupplier = await _supplierRepo.AddAsync(supplier);
            return _mapper.Map<SupplierViewModel>(createdSupplier);
        }

        public async Task<SupplierViewModel> UpdateSupplierAsync(int id, SupplierUpdateModel model)
        {
            
            if (!string.IsNullOrEmpty(model.Phone) && await _supplierRepo.PhoneExistsAsync(model.Phone))
            {
                throw new InvalidOperationException("❌ يوجد مورد مسجل بنفس رقم الهاتف بالفعل");
            }
            var existingSupplier = await _supplierRepo.GetByIdAsync(id);
            if (existingSupplier == null)
                throw new InvalidOperationException("❌ المورد غير موجود");

            var allSuppliers = await _supplierRepo.GetAllAsync();
            if (!string.IsNullOrEmpty(model.Phone) && allSuppliers.Any(s => s.Phone == model.Phone && s.Id != id))
            {
                throw new InvalidOperationException("❌ رقم الهاتف مستخدم بالفعل من مورد آخر");
            }

            _mapper.Map(model, existingSupplier);
            var updatedSupplier = await _supplierRepo.UpdateAsync(existingSupplier);
            return _mapper.Map<SupplierViewModel>(updatedSupplier);
        }

        public async Task DeleteSupplierAsync(int id)
        {
            // 1. Check for related purchase invoices directly in the database
            bool hasPurchaseInvoices = await _context.PurchaseInvoices
                                               .AnyAsync(inv => inv.SupplierId == id); // Add && inv.IsActive if using soft delete

            // 2. Prevent deletion if invoices exist
            if (hasPurchaseInvoices)
            {
                throw new InvalidOperationException("❌ لا يمكن حذف المورد لأنه مرتبط بفواتير مشتريات.");
            }

            // 3. Check if the supplier exists before trying to delete
            var supplierToDelete = await _supplierRepo.GetByIdAsync(id); // Or however you fetch it
            if (supplierToDelete == null)
            {
                throw new InvalidOperationException("❌ المورد المراد حذفه غير موجود أصلاً.");
            }

            // 4. Proceed with deletion if no invoices found and supplier exists
            await _supplierRepo.DeleteAsync(id);
            // OR if using soft delete:
            // supplierToDelete.IsActive = false;
            // _supplierRepo.Update(supplierToDelete); // Assuming Update saves changes or you call SaveChangesAsync after
            // await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<SupplierViewModel>> SearchSuppliersAsync(string searchTerm)
        {
            var suppliers = string.IsNullOrEmpty(searchTerm)
                ? await _supplierRepo.GetAllAsync()
                : await _supplierRepo.SearchAsync(searchTerm);

            return _mapper.Map<IEnumerable<SupplierViewModel>>(suppliers);
        }

        public async Task<IEnumerable<SupplierViewModel>> GetSuppliersWithBalanceAsync()
        {
            var suppliers = await _supplierRepo.GetSuppliersWithBalanceAsync();
            return _mapper.Map<IEnumerable<SupplierViewModel>>(suppliers);
        }


        public async Task ReactivateSupplierAsync(int id)
        {
            var supplier = await _supplierRepo.GetByIdAsync(id);
            if (supplier == null)
                throw new InvalidOperationException("المورد غير موجود");

            if (supplier.IsActive)
                throw new InvalidOperationException("المورد نشط بالفعل");

            supplier.IsActive = true;
            await _supplierRepo.UpdateAsync(supplier);
        }

        public IQueryable<Supplier> GetSuppliersAsQueryable()
        {
            return _supplierRepo.GetAsQueryable();
        }
    }
}