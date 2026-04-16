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
            ValidateReservationItems(model.ClientId, model.Items);

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
                    Status = ReservationStatus.Active,
                    Notes = model.Notes
                };

                var materialsById = await LoadAndValidateReservationMaterialsAsync(model.Items);

                foreach (var item in model.Items)
                {
                    var material = materialsById[item.MaterialId];
                    material.ReservedQuantity += item.Quantity;

                    reservation.ReservationItems.Add(new ReservationItem
                    {
                        MaterialId = item.MaterialId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.Quantity * item.UnitPrice
                    });
                }

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

                var client = await _clientRepo.GetByIdForUpdateAsync(reservation.ClientId);
                if (client == null) throw new Exception("العميل المرتبط بالحجز غير موجود.");

                var salesInvoice = new SalesInvoice
                {
                    ClientId = reservation.ClientId,
                    InvoiceNumber = $"INV-RES-{reservation.ReservationNumber}",
                    InvoiceDate = DateTime.Now,
                    TotalAmount = 0,
                    PaidAmount = 0,
                    RemainingAmount = 0
                };

                decimal invoiceTotal = 0;
                foreach (var item in reservation.ReservationItems)
                {
                    var material = await _materialRepo.GetByIdForUpdateAsync(item.MaterialId);
                    if (material == null) throw new Exception("مادة غير موجودة.");

                    var quantityRemaining = GetRemainingToFulfill(item);
                    if (quantityRemaining <= 0) continue;

                    ValidateFulfillmentQuantity(material, quantityRemaining);

                    material.Quantity -= quantityRemaining;
                    material.ReservedQuantity -= quantityRemaining;
                    item.FulfilledQuantity = item.Quantity;

                    var itemTotal = quantityRemaining * item.UnitPrice;
                    invoiceTotal += itemTotal;
                    salesInvoice.SalesInvoiceItems.Add(new SalesInvoiceItem
                    {
                        MaterialId = material.Id,
                        Quantity = quantityRemaining,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = itemTotal
                    });
                }

                if (invoiceTotal <= 0)
                    throw new Exception("لا توجد كميات متبقية لتسليم هذا الحجز.");

                salesInvoice.TotalAmount = invoiceTotal;
                salesInvoice.PaidAmount = 0;
                salesInvoice.RemainingAmount = invoiceTotal;
                client.Balance += invoiceTotal;
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

                foreach (var item in reservation.ReservationItems)
                {
                    var material = await _materialRepo.GetByIdForUpdateAsync(item.MaterialId);
                    if (material == null) throw new Exception("مادة غير موجودة.");

                    var remainingReserved = GetRemainingToFulfill(item);
                    ReleaseReservedQuantity(material, remainingReserved);
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

        private async Task<Dictionary<int, Material>> LoadAndValidateReservationMaterialsAsync(IEnumerable<ReservationItemModel> items)
        {
            var materialsById = new Dictionary<int, Material>();
            var requestedByMaterial = items
                .GroupBy(item => item.MaterialId)
                .Select(group => new
                {
                    MaterialId = group.Key,
                    RequestedQuantity = group.Sum(item => item.Quantity)
                });

            foreach (var requested in requestedByMaterial)
            {
                var material = await _materialRepo.GetByIdForUpdateAsync(requested.MaterialId);
                if (material == null)
                    throw new InvalidOperationException("مادة غير موجودة.");

                var availableQuantity = material.Quantity - material.ReservedQuantity;
                if (requested.RequestedQuantity > availableQuantity)
                    throw new InvalidOperationException($"لا يمكن حجز الكمية المطلوبة من المادة '{material.Name}'. المتاح: {availableQuantity:N2}.");

                materialsById[material.Id] = material;
            }

            return materialsById;
        }

        private static void ValidateReservationItems(int clientId, IReadOnlyCollection<ReservationItemModel> items)
        {
            if (clientId <= 0)
                throw new InvalidOperationException("يجب اختيار العميل.");

            if (items == null || !items.Any())
                throw new InvalidOperationException("يجب إضافة صنف واحد على الأقل للحجز.");

            foreach (var item in items)
            {
                if (item.MaterialId <= 0)
                    throw new InvalidOperationException("يجب اختيار مادة صحيحة لكل بند.");

                if (item.Quantity <= 0)
                    throw new InvalidOperationException("الكمية يجب أن تكون أكبر من الصفر.");

                if (item.UnitPrice <= 0)
                    throw new InvalidOperationException("سعر الوحدة يجب أن يكون أكبر من الصفر.");
            }
        }

        private static Dictionary<int, decimal> NormalizeFulfillments(IEnumerable<ReservationFulfillmentModel> fulfillments)
        {
            if (fulfillments == null)
                throw new InvalidOperationException("بيانات التسليم مطلوبة.");

            foreach (var fulfillment in fulfillments)
            {
                if (fulfillment.QuantityToFulfill < 0)
                    throw new InvalidOperationException("كمية التسليم لا يمكن أن تكون سالبة.");
            }

            return fulfillments
                .Where(fulfillment => fulfillment.QuantityToFulfill > 0)
                .GroupBy(fulfillment => fulfillment.ReservationItemId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(fulfillment => fulfillment.QuantityToFulfill));
        }

        private static decimal GetFulfilledQuantity(ReservationItem item)
        {
            var fulfilledQuantity = item.FulfilledQuantity ?? 0;
            if (fulfilledQuantity < 0 || fulfilledQuantity > item.Quantity)
                throw new InvalidOperationException("بيانات الحجز غير متسقة. يرجى مراجعة السجل يدوياً.");

            return fulfilledQuantity;
        }

        private static decimal GetRemainingToFulfill(ReservationItem item)
        {
            return item.Quantity - GetFulfilledQuantity(item);
        }

        private static void ReleaseReservedQuantity(Material material, decimal quantity)
        {
            if (quantity <= 0)
                return;

            if (material == null)
                throw new InvalidOperationException("مادة غير موجودة.");

            if (material.ReservedQuantity < quantity)
                throw new InvalidOperationException($"لا يمكن تحرير الحجز لأن كمية المادة المحجوزة غير متسقة: {material.Name}.");

            material.ReservedQuantity -= quantity;
        }

        private static void ValidateFulfillmentQuantity(Material material, decimal quantity)
        {
            if (material == null)
                throw new InvalidOperationException("مادة غير موجودة.");

            if (quantity <= 0)
                throw new InvalidOperationException("كمية التسليم يجب أن تكون أكبر من الصفر.");

            if (material.Quantity < quantity)
                throw new InvalidOperationException($"لا يمكن تسليم الحجز. المخزون الفعلي للمادة '{material.Name}' هو {material.Quantity:N2} فقط.");

            if (material.ReservedQuantity < quantity)
                throw new InvalidOperationException($"لا يمكن تسليم الحجز لأن الكمية المحجوزة للمادة '{material.Name}' غير كافية.");
        }

        public async Task<ReservationDetailsViewModel> UpdateReservationAsync(ReservationUpdateModel model)
        {
            ValidateReservationItems(model.ClientId, model.Items);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingReservation = await _reservationRepo.GetByIdForUpdateAsync(model.Id);
                if (existingReservation == null) throw new InvalidOperationException("الحجز غير موجود.");
                if (existingReservation.Status != ReservationStatus.Active)
                    throw new InvalidOperationException("لا يمكن تعديل حجز غير نشط.");
                if (existingReservation.ReservationItems.Any(i => GetFulfilledQuantity(i) > 0))
                    throw new InvalidOperationException("لا يمكن تعديل حجز تم تسليم جزء منه. يرجى إلغاء أو إتمام الحجز وفق إجراء مستقل.");

                var client = await _clientRepo.GetByIdForUpdateAsync(model.ClientId);
                if (client == null) throw new InvalidOperationException("العميل غير موجود.");

                foreach (var item in existingReservation.ReservationItems)
                {
                    ReleaseReservedQuantity(item.Material, item.Quantity);
                }

                // 2. تحديث الحجز بالبيانات الجديدة
                _mapper.Map(model, existingReservation);
                existingReservation.ReservationItems.Clear(); // حذف الأصناف القديمة

                var materialsById = await LoadAndValidateReservationMaterialsAsync(model.Items);
                decimal newTotalAmount = 0;
                foreach (var itemModel in model.Items)
                {
                    var material = materialsById[itemModel.MaterialId];
                    material.ReservedQuantity += itemModel.Quantity;

                    newTotalAmount += itemModel.Quantity * itemModel.UnitPrice;
                    existingReservation.ReservationItems.Add(_mapper.Map<ReservationItem>(itemModel));
                }

                existingReservation.TotalAmount = newTotalAmount;

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

                var requestedFulfillments = NormalizeFulfillments(fulfillments);
                if (!requestedFulfillments.Any())
                    throw new InvalidOperationException("يجب إدخال كمية أكبر من الصفر لبند واحد على الأقل.");

                var client = await _clientRepo.GetByIdForUpdateAsync(reservation.ClientId);
                if (client == null) throw new Exception("العميل المرتبط بالحجز غير موجود.");

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
                foreach (var fulfillment in requestedFulfillments)
                {
                    var itemToUpdate = reservation.ReservationItems
                        .FirstOrDefault(i => i.Id == fulfillment.Key);

                    if (itemToUpdate == null)
                        throw new InvalidOperationException("بند الحجز المطلوب تسليمه غير موجود.");

                    var quantityToFulfill = fulfillment.Value;
                    var material = itemToUpdate.Material;
                    var remainingToFulfill = GetRemainingToFulfill(itemToUpdate);
                    if (quantityToFulfill > remainingToFulfill)
                        throw new InvalidOperationException("تجاوز كمية التسليم المتبقية.");

                    ValidateFulfillmentQuantity(material, quantityToFulfill);

                    // تحديث الكمية المسلمة في الحجز
                    itemToUpdate.FulfilledQuantity = GetFulfilledQuantity(itemToUpdate) + quantityToFulfill;

                    // خصم الكمية من المخزون الفعلي (موجود)
                    material.Quantity -= quantityToFulfill;

                    // (الكود الناقص) خصم الكمية من المخزون المحجوز
                    material.ReservedQuantity -= quantityToFulfill;


                    // (الكود الناقص) إضافة البند إلى فاتورة البيع
                    var itemTotalPrice = quantityToFulfill * itemToUpdate.UnitPrice;
                    salesInvoice.SalesInvoiceItems.Add(new SalesInvoiceItem
                    {
                        MaterialId = itemToUpdate.MaterialId,
                        Quantity = quantityToFulfill,
                        UnitPrice = itemToUpdate.UnitPrice,
                        TotalPrice = itemTotalPrice
                    });
                    invoiceTotal += itemTotalPrice;
                }

                salesInvoice.TotalAmount = invoiceTotal;
                salesInvoice.PaidAmount = 0;
                salesInvoice.RemainingAmount = invoiceTotal;
                client.Balance += invoiceTotal;
                _context.SalesInvoices.Add(salesInvoice);

                // 3. التحقق من اكتمال الحجز (Fulfilled/Partial)
                if (reservation.ReservationItems.All(i => i.Quantity == GetFulfilledQuantity(i)))
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
