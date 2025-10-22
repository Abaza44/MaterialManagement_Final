using MaterialManagement.BLL.ModelVM.Reservation;
using MaterialManagement.DAL.Entities; 
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface IReservationService
    {
        Task<ReservationIndexViewModel> CreateReservationAsync(ReservationCreateModel model);
        Task FulfillReservationAsync(int reservationId); // (هذه دالة التسليم الكلي القديمة)
        Task CancelReservationAsync(int reservationId);
        Task<ReservationDetailsViewModel?> GetReservationDetailsAsync(int id);
        Task<IEnumerable<Reservation>> GetAllActiveReservationsWithDetailsAsync();


        Task<ReservationGetForUpdateModel?> GetReservationForUpdateAsync(int id);
        Task<ReservationDetailsViewModel> UpdateReservationAsync(ReservationUpdateModel model);
        Task<ReservationFulfillmentViewModel?> GetReservationDetailsForFulfillmentAsync(int id);
        Task PartialFulfillReservationAsync(int reservationId, List<ReservationFulfillmentModel> fulfillments);
    }
}