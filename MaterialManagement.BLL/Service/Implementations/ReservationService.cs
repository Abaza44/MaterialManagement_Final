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
                var client = await _clientRepo.GetByIdForUpdateAsync(model.ClientId);
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
                    var material = await _materialRepo.GetByIdForUpdateAsync(item.MaterialId);
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
                    var material = await _materialRepo.GetByIdForUpdateAsync(item.MaterialId);

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

                // بدلاً من: var client = reservation.Client;
                var client = await _clientRepo.GetByIdForUpdateAsync(reservation.ClientId);
                if (client == null) throw new Exception("العميل المرتبط بالحجز غير موجود.");
                client.Balance += reservation.TotalAmount;

                foreach (var item in reservation.ReservationItems)
                {
                    // بدلاً من: var material = item.Material;
                    var material = await _materialRepo.GetByIdForUpdateAsync(item.MaterialId);
                    if (material == null) continue; // أو إطلاق خطأ
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

        public async Task<IEnumerable<Reservation>> GetAllActiveReservationsWithDetailsAsync()
        {
            return await _reservationRepo.GetAllActiveWithDetailsAsync();
        }
        public async Task<IEnumerable<ReservationIndexViewModel>> GetAllReservationsAsync()
        {
            var reservations = await _reservationRepo.GetAllAsync();
            return _mapper.Map<IEnumerable<ReservationIndexViewModel>>(reservations);
        }

        public async Task<ReservationDetailsViewModel> UpdateReservationAsync(ReservationUpdateModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingReservation = await _reservationRepo.GetByIdForUpdateAsync(model.Id);
                if (existingReservation == null) throw new InvalidOperationException("الحجز غير موجود.");

                // 1. عكس التأثير المخزني والمالي للحجز القديم
                var client = await _clientRepo.GetByIdForUpdateAsync(existingReservation.ClientId);

                client.Balance += existingReservation.TotalAmount; // عكس تأثير المبلغ المحجوز

                foreach (var item in existingReservation.ReservationItems)
                {
                    // إرجاع الكمية المحجوزة للمخزون الفعلي
                    item.Material.ReservedQuantity -= item.Quantity;
                }

                // 2. تحديث الحجز بالبيانات الجديدة
                _mapper.Map(model, existingReservation);
                existingReservation.ReservationItems.Clear(); // حذف الأصناف القديمة

                decimal newTotalAmount = 0;
                foreach (var itemModel in model.Items)
                {
                    var material = await _materialRepo.GetByIdForUpdateAsync(itemModel.MaterialId);
                    if (material == null) throw new InvalidOperationException($"المادة {itemModel.MaterialName} غير موجودة");

                    // 3. تطبيق الحجز الجديد
                    material.ReservedQuantity += itemModel.Quantity;

                    newTotalAmount += itemModel.Quantity * itemModel.UnitPrice;
                    existingReservation.ReservationItems.Add(_mapper.Map<ReservationItem>(itemModel));
                }

                existingReservation.TotalAmount = newTotalAmount;
                client.Balance -= newTotalAmount;

                _reservationRepo.Update(existingReservation);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return _mapper.Map<ReservationDetailsViewModel>(existingReservation);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ReservationGetForUpdateModel?> GetReservationForUpdateAsync(int id)
        {
            // 1. جلب الحجز من Repository (نستخدم الدالة التي تجلب كل التفاصيل)
            var reservation = await _reservationRepo.GetByIdForUpdateAsync(id);

            if (reservation == null) return null;

            // 2. التحقق من أن الحجز نشط (لا يمكن تعديل حجز تم تسليمه)
            if (reservation.Status != ReservationStatus.Active)
                throw new InvalidOperationException("لا يمكن تعديل حجز غير نشط (تم تسليمه أو إلغاؤه).");

            // 3. بناء نموذج ViewModel للعرض في صفحة التعديل
            var model = new ReservationGetForUpdateModel
            {
                Id = reservation.Id,
                ClientId = reservation.ClientId,
                Notes = reservation.Notes,
                // تحويل أصناف الحجز إلى قائمة موديل الأصناف
                Items = reservation.ReservationItems.Select(item => new ReservationItemModel
                {
                    MaterialId = item.MaterialId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    MaterialName = item.Material.Name // نعتمد على أن الـ Repo جلب اسم المادة
                }).ToList()
            };

            return model;
        }
        public async Task PartialFulfillReservationAsync(int reservationId, List<ReservationFulfillmentModel> fulfillments)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var reservation = await _reservationRepo.GetByIdForUpdateAsync(reservationId);
                if (reservation == null || reservation.Status != ReservationStatus.Active)
                    throw new Exception("الحجز غير موجود أو غير نشط.");

                // 1. إنشاء فاتورة بيع جديدة (الكود الناقص)
                var salesInvoice = new SalesInvoice
                {
                    ClientId = reservation.ClientId,
                    InvoiceNumber = $"INV-PART-{reservation.ReservationNumber}-{DateTime.Now.Ticks}",
                    InvoiceDate = DateTime.Now,
                    TotalAmount = 0, 
                    PaidAmount = 0,
                    RemainingAmount = 0
                };

                decimal invoiceTotal = 0;
                foreach (var fulfillment in fulfillments)
                {
                    if (fulfillment.QuantityToFulfill <= 0) continue; // تجاهل الكميات الصفرية

                    var itemToUpdate = reservation.ReservationItems
                        .FirstOrDefault(i => i.Id == fulfillment.ReservationItemId);

                    if (itemToUpdate == null) continue;

                    var material = itemToUpdate.Material; // تم جلبه مسبقاً

                    // ... (التحققات الموجودة) ...
                    if (itemToUpdate.Quantity - itemToUpdate.FulfilledQuantity < fulfillment.QuantityToFulfill)
                        throw new InvalidOperationException("تجاوز كمية التسليم المتبقية.");

                    // تحديث الكمية المسلمة في الحجز
                    itemToUpdate.FulfilledQuantity += fulfillment.QuantityToFulfill;

                    // خصم الكمية من المخزون الفعلي (موجود)
                    material.Quantity -= fulfillment.QuantityToFulfill;

                    // (الكود الناقص) خصم الكمية من المخزون المحجوز
                    material.ReservedQuantity -= fulfillment.QuantityToFulfill;


                    // (الكود الناقص) إضافة البند إلى فاتورة البيع
                    var itemTotalPrice = fulfillment.QuantityToFulfill * itemToUpdate.UnitPrice;
                    salesInvoice.SalesInvoiceItems.Add(new SalesInvoiceItem
                    {
                        MaterialId = itemToUpdate.MaterialId,
                        Quantity = fulfillment.QuantityToFulfill,
                        UnitPrice = itemToUpdate.UnitPrice,
                        TotalPrice = itemTotalPrice
                    });
                    invoiceTotal += itemTotalPrice;
                }

                salesInvoice.TotalAmount = invoiceTotal;
                salesInvoice.PaidAmount = invoiceTotal; 
                _context.SalesInvoices.Add(salesInvoice);

                // 3. التحقق من اكتمال الحجز (Fulfilled/Partial)
                if (reservation.ReservationItems.All(i => i.Quantity == i.FulfilledQuantity))
                {
                    reservation.Status = ReservationStatus.Fulfilled;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ReservationFulfillmentViewModel?> GetReservationDetailsForFulfillmentAsync(int id)
        {
            var reservation = await _reservationRepo.GetByIdWithDetailsAsync(id);
            if (reservation == null || reservation.Status != ReservationStatus.Active)
                return null;

            // 3. قم بالتحويل إلى ReservationFulfillmentViewModel بدلاً من ReservationDetailsViewModel
            var viewModel = new ReservationFulfillmentViewModel
            {
                ReservationId = reservation.Id,
                ReservationNumber = reservation.ReservationNumber,
                ClientName = reservation.Client.Name,
                ItemsToFulfill = reservation.ReservationItems
                    .Where(i => i.Quantity > (i.FulfilledQuantity ?? 0)) 
                    .Select(i => new FulfillmentItemModel
                    {
                        ReservationItemId = i.Id,
                        MaterialId = i.MaterialId,
                        MaterialName = i.Material.Name,
                        QuantityReserved = i.Quantity,
                        QuantityFulfilled = i.FulfilledQuantity ?? 0,
                        QuantityRemaining = i.Quantity - (i.FulfilledQuantity ?? 0)
                    }).ToList()
            };

            return viewModel;
        }
    }
}