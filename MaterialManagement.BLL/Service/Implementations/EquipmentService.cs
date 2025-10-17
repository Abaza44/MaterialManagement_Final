using AutoMapper;
using AutoMapper.QueryableExtensions;
using MaterialManagement.BLL.ModelVM.Equipment;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class EquipmentService : IEquipmentService
    {
        private readonly IEquipmentRepo _equipmentRepo;
        private readonly IMapper _mapper;
        public EquipmentService(IEquipmentRepo equipmentRepo, IMapper mapper) { _equipmentRepo = equipmentRepo; _mapper = mapper; }

        public async Task<IEnumerable<EquipmentViewModel>> GetAllEquipmentAsync()
        {
            var equipmentEntities = await _equipmentRepo.GetAllAsync();
            var viewModels = _mapper.Map<IEnumerable<EquipmentViewModel>>(equipmentEntities);

            foreach (var vm in viewModels)
            {
                var entity = equipmentEntities.First(e => e.Code == vm.Code);
                if (entity.MaintenanceHistory.Any())
                {
                    vm.LastMaintenanceDate = entity.MaintenanceHistory.OrderByDescending(m => m.MaintenanceDate).First().MaintenanceDate;
                }
            }


            return viewModels;
        }

        public async Task<EquipmentViewModel?> GetByCodeAsync(int code)
        {
            var equipment = await _equipmentRepo.GetByCodeAsync(code);
            return _mapper.Map<EquipmentViewModel>(equipment); 
        }

        public async Task<EquipmentViewModel> CreateEquipmentAsync(EquipmentCreateModel model)
        {

            var equipment = _mapper.Map<Equipment>(model);


            if (model.LastMaintenanceDate.HasValue)
            {

                var initialMaintenance = new MaintenanceRecord
                {
                    MaintenanceDate = model.LastMaintenanceDate.Value,
                    Description = "صيانة مبدئية عند إضافة المعدة",
                    Cost = 0
                };

                equipment.MaintenanceHistory.Add(initialMaintenance);
            }

            var created = await _equipmentRepo.CreateAsync(equipment);
            return _mapper.Map<EquipmentViewModel>(created);
        }

        public async Task<EquipmentViewModel> UpdateEquipmentAsync(EquipmentUpdateModel model)
        {

            var equipmentToUpdate = await _equipmentRepo.GetByCodeForUpdateAsync(model.Code);

            if (equipmentToUpdate == null)
                throw new InvalidOperationException("المعدة غير موجودة");


            _mapper.Map(model, equipmentToUpdate);

            await _equipmentRepo.UpdateAsync(equipmentToUpdate);

            return _mapper.Map<EquipmentViewModel>(equipmentToUpdate);
        }

        public async Task<bool> DeleteEquipmentAsync(int code)
        {
            return await _equipmentRepo.DeleteAsync(code);
        }

        public async Task<EquipmentUpdateModel?> GetEquipmentForUpdateAsync(int code)
        {
            var equipment = await _equipmentRepo.GetByCodeAsync(code);
            return _mapper.Map<EquipmentUpdateModel>(equipment);
        }
        public IQueryable<EquipmentViewModel> GetEquipmentAsQueryable()
        {
            
            var equipment = _equipmentRepo.GetAsQueryable();

            return equipment.ProjectTo<EquipmentViewModel>(_mapper.ConfigurationProvider);
        }
    }
}