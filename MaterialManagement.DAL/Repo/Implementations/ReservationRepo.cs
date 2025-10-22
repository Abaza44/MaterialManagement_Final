using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Enums;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class ReservationRepo : IReservationRepo
    {
        private readonly MaterialManagementContext _context;

        public ReservationRepo(MaterialManagementContext context)
        {
            _context = context;
        }

        public async Task<Reservation> AddAsync(Reservation reservation)
        {
            await _context.Reservations.AddAsync(reservation);
            
            return reservation;
        }

        public async Task<IEnumerable<Reservation>> GetAllActiveWithDetailsAsync()
        {
            return await _context.Reservations
                .Where(r => r.Status == ReservationStatus.Active)
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(item => item.Material)
                    .AsNoTracking()
                .OrderBy(r => r.Client.Name).ThenByDescending(r => r.ReservationDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Reservation>> GetAllAsync()
        {
            return await _context.Reservations
                .Include(r => r.Client)
                .AsNoTracking() // <<< تحسين الأداء
                .OrderByDescending(r => r.ReservationDate)
                .ToListAsync();
        }

        public async Task<Reservation?> GetByIdForUpdateAsync(int id)
        {
            return await _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(item => item.Material)
                
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<Reservation?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(item => item.Material)
                .AsNoTracking() 
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public void Update(Reservation reservation)
        {
            _context.Reservations.Update(reservation);
        }

        
    }
}