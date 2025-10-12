using AutoMapper;
using MaterialManagement.BLL.ModelVM.Reservation;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Enums;
using MaterialManagement.DAL.Repo.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class ReservationService : IReservationService
    {
        private readonly IReservationRepo _reservationRepo;
        private readonly IMaterialRepo _materialRepo;
        private readonly IClientRepo _clientRepo;
        private readonly MaterialManagementContext _context;
        private readonly IMapper _mapper;

        public ReservationService(IReservationRepo reservationRepo, IMaterialRepo materialRepo, IClientRepo clientRepo, MaterialManagementContext context, IMapper mapper)
        {
            _reservationRepo = reservationRepo;
            _materialRepo = materialRepo;
            _clientRepo = clientRepo;
            _context = context;
            _mapper = mapper;
        }

        public async Task<ReservationIndexViewModel> CreateReservationAsync(ReservationCreateModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var client = await _clientRepo.GetByIdAsync(model.ClientId);
                if (client == null) throw new Exception("العميل غير موجود.");

                var totalAmount = model.Items.Sum(i => i.Quantity * i.UnitPrice);

                var reservation = new Reservation
                {
                    ClientId = model.ClientId,
                    ReservationNumber = $"RES-{DateTime.Now.Ticks}",
                    TotalAmount = totalAmount,
                    Status = ReservationStatus.Active
                };

                foreach (var item in model.Items)
                {
                    var material = await _materialRepo.GetByIdAsync(item.MaterialId);
                    if (material == null) throw new Exception("مادة غير موجودة.");

                   

                    material.ReservedQuantity += item.Quantity;

                    reservation.ReservationItems.Add(new ReservationItem
                    {
                        MaterialId = item.MaterialId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.Quantity * item.UnitPrice
                    });
                }

                client.Balance -= totalAmount;

                await _reservationRepo.AddAsync(reservation);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var viewModel = _mapper.Map<ReservationIndexViewModel>(reservation);
                viewModel.ClientName = client.Name; // AutoMapper needs help with nested properties
                return viewModel;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task FulfillReservationAsync(int reservationId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var reservation = await _reservationRepo.GetByIdForUpdateAsync(reservationId);
                if (reservation == null || reservation.Status != ReservationStatus.Active)
                    throw new Exception("الحجز غير موجود أو تم التعامل معه بالفعل.");

                var salesInvoice = new SalesInvoice
                {
                    ClientId = reservation.ClientId,
                    InvoiceNumber = $"INV-RES-{reservation.ReservationNumber}",
                    InvoiceDate = DateTime.Now,
                    TotalAmount = reservation.TotalAmount,
                    PaidAmount = reservation.TotalAmount,
                    RemainingAmount = 0
                };

                foreach (var item in reservation.ReservationItems)
                {
                    var material = item.Material;

                    if (material.Quantity < item.Quantity)
                    {
                        throw new Exception($"لا يمكن تسليم الحجز. المخزون الفعلي للمادة '{material.Name}' هو {material.Quantity} فقط.");
                    }
                    material.Quantity -= item.Quantity;
                    material.ReservedQuantity -= item.Quantity;

                    salesInvoice.SalesInvoiceItems.Add(new SalesInvoiceItem
                    {
                        MaterialId = item.MaterialId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.TotalPrice
                    });
                }

                reservation.Status = ReservationStatus.Fulfilled;

                _context.SalesInvoices.Add(salesInvoice);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task CancelReservationAsync(int reservationId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var reservation = await _reservationRepo.GetByIdForUpdateAsync(reservationId);
                if (reservation == null || reservation.Status != ReservationStatus.Active)
                    throw new Exception("الحجز غير موجود أو لا يمكن إلغاؤه.");

                var client = reservation.Client;
                client.Balance += reservation.TotalAmount;

                foreach (var item in reservation.ReservationItems)
                {
                    var material = item.Material;
                    material.ReservedQuantity -= item.Quantity;
                }

                reservation.Status = ReservationStatus.Cancelled;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // أضف هذه الدالة الجديدة داخل كلاس ReservationService

        public async Task<ReservationDetailsViewModel?> GetReservationDetailsAsync(int id)
        {
            var reservation = await _reservationRepo.GetByIdWithDetailsAsync(id);
            if (reservation == null) return null;

            return _mapper.Map<ReservationDetailsViewModel>(reservation); 
        }
        public async Task<IEnumerable<ReservationIndexViewModel>> GetAllReservationsAsync()
        {
            var reservations = await _reservationRepo.GetAllAsync();
            return _mapper.Map<IEnumerable<ReservationIndexViewModel>>(reservations);
        }

        public async Task<IEnumerable<Reservation>> GetAllActiveReservationsWithDetailsAsync()
        {
            return await _reservationRepo.GetAllActiveWithDetailsAsync();
        }
    }
}