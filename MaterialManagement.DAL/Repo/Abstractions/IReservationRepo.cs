using MaterialManagement.DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface IReservationRepo
    {
        Task<Reservation> AddAsync(Reservation reservation);
        Task<Reservation?> GetByIdWithDetailsAsync(int id);
        Task<IEnumerable<Reservation>> GetAllAsync();

        Task<Reservation?> GetByIdForUpdateAsync(int id);
        // ستحتاج لدالة Update لاحقًا لتغيير حالة الحجز
        Task<IEnumerable<Reservation>> GetAllActiveWithDetailsAsync();
        void Update(Reservation reservation);
    }
}