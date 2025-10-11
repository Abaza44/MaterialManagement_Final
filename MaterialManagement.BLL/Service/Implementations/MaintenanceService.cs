using AutoMapper;
using MaterialManagement.BLL.ModelVM.Expense; // <-- أضف هذا
using MaterialManagement.BLL.ModelVM.Maintenance;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB; // <-- أضف هذا
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly MaterialManagementContext _context;
        private readonly IMaintenanceRecordRepo _maintenanceRepo;
        private readonly IEquipmentRepo _equipmentRepo;
        private readonly IMapper _mapper;

        // <<< تم تحديث الـ Constructor >>>
        public MaintenanceService(
            MaterialManagementContext context,
            IMaintenanceRecordRepo maintenanceRepo,
            IEquipmentRepo equipmentRepo,
            IMapper mapper)
        {
            _context = context;
            _maintenanceRepo = maintenanceRepo;
            _equipmentRepo = equipmentRepo;
            _mapper = mapper;
        }

        public async Task<IEnumerable<MaintenanceRecordViewModel>> GetHistoryForEquipmentAsync(int equipmentCode)
        {
            var records = await _maintenanceRepo.GetByEquipmentCodeAsync(equipmentCode);
            return _mapper.Map<IEnumerable<MaintenanceRecordViewModel>>(records);
        }

        
        public async Task<MaintenanceRecordViewModel> AddMaintenanceRecordAsync(MaintenanceRecordCreateModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                
                var record = _mapper.Map<MaintenanceRecord>(model);
                _context.MaintenanceRecords.Add(record);

                
                if (model.Cost > 0)
                {
                    var equipment = await _equipmentRepo.GetByCodeAsync(model.EquipmentCode);
                    var expense = new Expense
                    {
                        Description = $"صيانة للمعدة: {equipment?.Name} - {model.Description}",
                        Amount = model.Cost,
                        ExpenseDate = model.MaintenanceDate,
                        Category = "صيانة",
                        PaymentTo = model.PerformedBy,
                        IsActive = true,
                        CreatedDate = System.DateTime.Now
                    };
                    _context.Expenses.Add(expense);
                }

                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return _mapper.Map<MaintenanceRecordViewModel>(record);
            }
            catch (System.Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}