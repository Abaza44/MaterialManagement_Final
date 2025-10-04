using AutoMapper;
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
            return _mapper.Map<IEnumerable<EquipmentViewModel>>(await _equipmentRepo.GetAllAsync());
        }

        public async Task<EquipmentViewModel?> GetByCodeAsync(int code)
        {
            return _mapper.Map<EquipmentViewModel>(await _equipmentRepo.GetByCodeAsync(code));
        }

        public async Task<EquipmentViewModel> CreateEquipmentAsync(EquipmentCreateModel model)
        {
            var equipment = _mapper.Map<Equipment>(model);
            var created = await _equipmentRepo.CreateAsync(equipment);
            return _mapper.Map<EquipmentViewModel>(created);
        }

        public async Task<EquipmentViewModel> UpdateEquipmentAsync(EquipmentUpdateModel model)
        {
            var equipmentToUpdate = await _equipmentRepo.GetByCodeAsync(model.Code);
            if (equipmentToUpdate == null)
                throw new InvalidOperationException("Equipment not found");

            _mapper.Map(model, equipmentToUpdate);
            await _equipmentRepo.UpdateAsync(equipmentToUpdate);
            return _mapper.Map<EquipmentViewModel>(equipmentToUpdate);
        }

        public async Task<bool> DeleteEquipmentAsync(int code)
        {
            return await _equipmentRepo.DeleteAsync(code);
        }
    }
}