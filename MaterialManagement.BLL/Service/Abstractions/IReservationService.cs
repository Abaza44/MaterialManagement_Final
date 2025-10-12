using MaterialManagement.BLL.ModelVM.Reservation;
using MaterialManagement.DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface IReservationService
    {
        Task<ReservationIndexViewModel> CreateReservationAsync(ReservationCreateModel model);
        Task FulfillReservationAsync(int reservationId);
        Task CancelReservationAsync(int reservationId); 
        Task<IEnumerable<ReservationIndexViewModel>> GetAllReservationsAsync();

        Task<ReservationDetailsViewModel?> GetReservationDetailsAsync(int id);
        Task<IEnumerable<Reservation>> GetAllActiveReservationsWithDetailsAsync();
    }
}