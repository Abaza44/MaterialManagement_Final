using AutoMapper;
using MaterialManagement.BLL.ModelVM.Maintenance;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly IMaintenanceRecordRepo _maintenanceRepo;
        private readonly IEquipmentRepo _equipmentRepo; // نحتاجه للحصول على اسم المعدة
        private readonly IMapper _mapper;

        public MaintenanceService(IMaintenanceRecordRepo maintenanceRepo, IEquipmentRepo equipmentRepo, IMapper mapper)
        {
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
            var record = _mapper.Map<MaintenanceRecord>(model);
            var createdRecord = await _maintenanceRepo.CreateAsync(record);

            // نحتاج جلب اسم المعدة لعرضه في الـ ViewModel
            var equipment = await _equipmentRepo.GetByCodeAsync(createdRecord.EquipmentCode);
            var viewModel = _mapper.Map<MaintenanceRecordViewModel>(createdRecord);
            viewModel.EquipmentName = equipment?.Name ?? "غير معروف";

            return viewModel;
        }
    }
}