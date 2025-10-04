using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class MaintenanceRecordRepo : IMaintenanceRecordRepo
    {
        private readonly MaterialManagementContext _context;
        public MaintenanceRecordRepo(MaterialManagementContext context) { _context = context; }

        public async Task<IEnumerable<MaintenanceRecord>> GetByEquipmentCodeAsync(int equipmentCode)
        {
            return await _context.MaintenanceRecords
                .Where(r => r.EquipmentCode == equipmentCode)
                .OrderByDescending(r => r.MaintenanceDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<MaintenanceRecord> CreateAsync(MaintenanceRecord record)
        {
            _context.MaintenanceRecords.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<MaintenanceRecord?> GetByIdAsync(int id)
        {
            return await _context.MaintenanceRecords.FindAsync(id);
        }

        public async Task UpdateAsync(MaintenanceRecord record)
        {
            _context.MaintenanceRecords.Update(record);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var record = await GetByIdAsync(id);
            if (record == null) return false;

            _context.MaintenanceRecords.Remove(record);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}