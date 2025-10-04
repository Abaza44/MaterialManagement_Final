using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class EquipmentRepo : IEquipmentRepo
    {
        private readonly MaterialManagementContext _context;
        public EquipmentRepo(MaterialManagementContext context) { _context = context; }

        public async Task<IEnumerable<Equipment>> GetAllAsync()
        {
            return await _context.Equipment.AsNoTracking().OrderBy(e => e.Name).ToListAsync();
        }

        // في دالة GetByCodeAsync
        public async Task<Equipment?> GetByCodeAsync(int code)
        {
            return await _context.Equipment
                .Include(e => e.MaintenanceHistory) // <-- أضف هذا السطر
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Code == code);
        }

        public async Task<Equipment> CreateAsync(Equipment equipment)
        {
            _context.Equipment.Add(equipment);
            await _context.SaveChangesAsync();
            return equipment;
        }

        public async Task UpdateAsync(Equipment equipment)
        {
            _context.Entry(equipment).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int code)
        {
            var equipment = await _context.Equipment.FindAsync(code);
            if (equipment == null) return false;

            _context.Equipment.Remove(equipment);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}