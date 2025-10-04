using AutoMapper;
using MaterialManagement.BLL.ModelVM.Material;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class MaterialService : IMaterialService
    {
        private readonly MaterialManagementContext _context; // <<< تم إضافته هنا
        private readonly IMaterialRepo _materialRepo;
        private readonly IMapper _mapper;

        public MaterialService(
            MaterialManagementContext context, // <<< تم إضافته هنا
            IMaterialRepo materialRepo,
            IMapper mapper)
        {
            _context = context; // <<< تم إضافته هنا
            _materialRepo = materialRepo;
            _mapper = mapper;
        }
        public async Task<IEnumerable<MaterialViewModel>> GetAllMaterialsAsync()
        {
            var materials = await _context.Materials.AsNoTracking().OrderBy(m => m.Name).ToListAsync();
            return _mapper.Map<IEnumerable<MaterialViewModel>>(materials);
        }

        public async Task<MaterialViewModel?> GetMaterialByIdAsync(int id)
        {
            var material = await _context.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            return _mapper.Map<MaterialViewModel>(material);
        }

        public async Task<MaterialViewModel> CreateMaterialAsync(MaterialCreateModel model)
        {
            // 1. إذا أدخل المستخدم كودًا، تحقق من أنه غير مكرر
            if (!string.IsNullOrWhiteSpace(model.Code))
            {
                if (await _context.Materials.AnyAsync(m => m.Code == model.Code))
                {
                    throw new InvalidOperationException($"الكود '{model.Code}' مستخدم بالفعل لمادة أخرى.");
                }
            }
            // 2. إذا ترك المستخدم الكود فارغًا، قم بإنشاء كود تلقائي
            else
            {
                var allCodes = await _context.Materials
                                     .Select(m => m.Code)
                                     .ToListAsync();

                // ب. الآن، قم بالعمليات على القائمة في الذاكرة
                var maxCodeNumber = allCodes
                                        .Select(code => int.TryParse(code, out int num) ? num : 0)
                                        .DefaultIfEmpty(0)
                                        .Max();

                model.Code = (maxCodeNumber + 1).ToString();
            }

            var material = _mapper.Map<Material>(model);
            material.CreatedDate = DateTime.Now;

            _context.Materials.Add(material);
            await _context.SaveChangesAsync();

            return _mapper.Map<MaterialViewModel>(material);
        }

        public async Task<MaterialViewModel> UpdateMaterialAsync(int id, MaterialUpdateModel model)
        {
            var materialToUpdate = await _context.Materials.FindAsync(id);
            if (materialToUpdate == null)
                throw new InvalidOperationException("المادة غير موجودة");

            // التحقق من تكرار الكود إذا تم تغييره
            if (materialToUpdate.Code != model.Code)
            {
                if (await _context.Materials.AnyAsync(m => m.Code == model.Code && m.Id != id))
                {
                    throw new InvalidOperationException("كود المادة موجود بالفعل");
                }
            }

            _mapper.Map(model, materialToUpdate);
            await _context.SaveChangesAsync();
            return _mapper.Map<MaterialViewModel>(materialToUpdate);
        }

        public async Task DeleteMaterialAsync(int id)
        {
            var material = await _context.Materials.FindAsync(id);
            if (material == null)
                throw new InvalidOperationException("المادة غير موجودة");

            material.IsActive = false; // Soft Delete
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<MaterialViewModel>> SearchMaterialsAsync(string searchTerm)
        {
            var materials = await _context.Materials
                .Where(m => m.Name.Contains(searchTerm) || m.Code.Contains(searchTerm))
                .AsNoTracking()
                .ToListAsync();
            return _mapper.Map<IEnumerable<MaterialViewModel>>(materials);
        }

        public async Task<IEnumerable<MaterialViewModel>> GetLowStockMaterialsAsync()
        {
            var materials = await _context.Materials
                .Where(m => m.Quantity <= 0) // يمكنك تغيير هذا إلى m.MinimumQuantity
                .AsNoTracking()
                .ToListAsync();
            return _mapper.Map<IEnumerable<MaterialViewModel>>(materials);
        }

        public async Task UpdateMaterialQuantityAsync(int materialId, decimal quantity, bool isAddition)
        {
            var material = await _materialRepo.GetByIdAsync(materialId);
            if (material == null)
                throw new InvalidOperationException("المادة غير موجودة");

            if (isAddition)
            {
                material.Quantity += quantity;
            }
            else
            {
                if (material.Quantity < quantity)
                    throw new InvalidOperationException("الكمية المطلوبة غير متوفرة في المخزن");
                
                material.Quantity -= quantity;
            }

            await _materialRepo.UpdateAsync(material);
        }

        public async Task<bool> IsCodeExistsAsync(string code, int? excludeId = null)
        {
            var existingMaterial = await _materialRepo.GetByCodeAsync(code);
            return existingMaterial != null && (excludeId == null || existingMaterial.Id != excludeId);
        }
    }
}