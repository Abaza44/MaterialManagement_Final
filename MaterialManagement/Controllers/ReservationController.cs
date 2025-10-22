using MaterialManagement.BLL.ModelVM.Reservation;
using MaterialManagement.BLL.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    public class ReservationController : Controller
    {
        private readonly IReservationService _reservationService;
        private readonly IClientService _clientService;
        private readonly IMaterialService _materialService;

        public ReservationController(
            IReservationService reservationService,
            IClientService clientService,
            IMaterialService materialService)
        {
            _reservationService = reservationService;
            _clientService = clientService;
            _materialService = materialService;
        }

        // ===========================================
        // 🔹 عرض القائمة والبيانات (Index & Details)
        // ===========================================

        // GET: /Reservation
        public async Task<IActionResult> Index()
        {
            var allReservations = await _reservationService.GetAllActiveReservationsWithDetailsAsync();
            var groupedByClient = allReservations
                .GroupBy(res => res.Client)
                .Select(group => new ClientReservationsViewModel
                {
                    ClientId = group.Key.Id,
                    ClientName = group.Key.Name,
                    Reservations = group.Select(res => new ReservationSummaryViewModel
                    {
                        Id = res.Id,
                        ReservationNumber = res.ReservationNumber,
                        ReservationDate = res.ReservationDate,
                        TotalAmount = res.TotalAmount,
                        ItemsSummary = string.Join(", ", res.ReservationItems.Select(item => $"{item.Material.Name} ({item.Quantity})"))
                    }).ToList()
                })
                .OrderBy(c => c.ClientName)
                .ToList();
            return View(groupedByClient);
        }

        // GET: /Reservation/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var model = await _reservationService.GetReservationDetailsAsync(id);
            if (model == null) return NotFound();
            return View(model);
        }

        // ===========================================
        // 🔹 الإنشاء والتعديل (Create & Edit)
        // ===========================================

        // GET: /Reservation/Create
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            ViewBag.IsUpdate = false;
            return View(new ReservationGetForUpdateModel()); // <-- الكود الجديد
        }

        // POST: /Reservation/Create
        // POST: /Reservation/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReservationCreateModel model)
        {
            if (ModelState.IsValid && model.Items.Count > 0)
            {
                try
                {
                    await _reservationService.CreateReservationAsync(model);
                    TempData["SuccessMessage"] = "تم إنشاء الحجز بنجاح!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex) { ModelState.AddModelError(string.Empty, $"حدث خطأ: {ex.Message}"); }
            }

            if (model.Items.Count == 0) { ModelState.AddModelError("Items", "يجب إضافة صنف واحد على الأقل."); }

            await PopulateDropdowns(model.ClientId);
            ViewBag.IsUpdate = false;

            // ▼▼▼ هذا هو الإصلاح ▼▼▼
            // تحويل الموديل من "CreateModel" إلى "GetForUpdateModel"
            var createViewModel = new ReservationGetForUpdateModel
            {
                ClientId = model.ClientId,
                Notes = model.Notes,
                Items = model.Items
            };

            return View(createViewModel); // <--- إرسال الموديل الصحيح
        }

        // GET: /Reservation/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _reservationService.GetReservationForUpdateAsync(id);
            if (model == null) return NotFound();

            await PopulateDropdowns(model.ClientId);
            ViewBag.IsUpdate = true;
            return View("Create", model); // استخدام نفس واجهة الإنشاء
        }

        // POST: /Reservation/Edit/5
        // POST: /Reservation/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ReservationUpdateModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _reservationService.UpdateReservationAsync(model);
                    TempData["SuccessMessage"] = "تم تحديث الحجز بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"فشل التحديث: {ex.Message}");
                }
            }

            // إذا فشل التحقق أو حدث خطأ، نصل إلى هنا
            await PopulateDropdowns(model.ClientId);
            ViewBag.IsUpdate = true;

            // ▼▼▼ هذا هو الإصلاح ▼▼▼
            // لا تقم بإرسال "model" مباشرة
            // قم بإنشاء الموديل الصحيح الذي يتوقعه الـ View
            var updateViewModel = new ReservationGetForUpdateModel
            {
                Id = model.Id,
                ClientId = model.ClientId,
                Notes = model.Notes,
                Items = model.Items // قائمة الأصناف موجودة بالفعل في الموديل
            };

            return View("Create", updateViewModel); // <--- إرسال الموديل الصحيح
        }

        // ===========================================
        // 🔹 التسليم والإلغاء (Fulfill & Cancel)
        // ===========================================

        // GET: /Reservation/Fulfill/5 (للتسليم الجزئي)
        [HttpGet]
        public async Task<IActionResult> Fulfill(int id)
        {
            var reservation = await _reservationService.GetReservationDetailsForFulfillmentAsync(id);
            if (reservation == null) return NotFound();

            return View(reservation);
        }

        // POST: /Reservation/PartialFulfill/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Fulfill(ReservationFulfillmentViewModel model)
        {
            // 1. الآن هذا السطر سيعمل بفاعلية بسبب الـ [Range] الذي أضفناه
            if (ModelState.IsValid)
            {
                // 2. فلترة الكميات التي أدخلها المستخدم (أكبر من صفر فقط)
                var itemsToFulfill = model.ItemsToFulfill
                    .Where(i => i.QuantityToFulfillNow > 0)
                    .Select(i => new ReservationFulfillmentModel
                    {
                        ReservationItemId = i.ReservationItemId,
                        QuantityToFulfill = i.QuantityToFulfillNow // <-- استخدام الخاصية الجديدة
                    }).ToList();

                // 3. التحقق إذا كان المستخدم أدخل أي كميات أصلاً
                if (!itemsToFulfill.Any())
                {
                    ModelState.AddModelError(string.Empty, "يجب إدخال كمية (أكبر من صفر) لبند واحد على الأقل.");
                    // (اذهب إلى الخطوة 5 لإعادة تحميل الصفحة)
                }
                else
                {
                    try
                    {
                        // 4. إرسال القائمة المفلترة فقط إلى الخدمة
                        await _reservationService.PartialFulfillReservationAsync(model.ReservationId, itemsToFulfill);
                        TempData["SuccessMessage"] = "تم تسليم جزئي للحجز وإنشاء فاتورة.";
                        return RedirectToAction(nameof(Details), new { id = model.ReservationId });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError(string.Empty, $"فشل التسليم الجزئي: {ex.Message}");
                        // (اذهب إلى الخطوة 5 لإعادة تحميل الصفحة)
                    }
                }
            }

            // 5. (مهم جداً) عند فشل الـ ModelState أو حدوث خطأ أو عدم إدخال كميات:
            // يجب إعادة تحميل الموديل من قاعدة البيانات لعرض البيانات الصحيحة (Stale Data)
            var failedModel = await _reservationService.GetReservationDetailsForFulfillmentAsync(model.ReservationId);
            if (failedModel == null) return NotFound();

            // (اختياري لكن موصى به): إعادة ملء المدخلات الخاطئة التي أدخلها المستخدم
            foreach (var item in failedModel.ItemsToFulfill)
            {
                var submittedItem = model.ItemsToFulfill.FirstOrDefault(i => i.ReservationItemId == item.ReservationItemId);
                if (submittedItem != null)
                {
                    // أعد القيمة الخاطئة ليرى المستخدم ما أدخله
                    item.QuantityToFulfillNow = submittedItem.QuantityToFulfillNow;
                }
            }

            return View(failedModel);
        }

        // POST: /Reservation/Cancel/5 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _reservationService.CancelReservationAsync(id);
                TempData["SuccessMessage"] = "تم إلغاء الحجز بنجاح!";
            }
            catch (Exception ex) { TempData["ErrorMessage"] = $"فشل إلغاء الحجز: {ex.Message}"; }
            return RedirectToAction(nameof(Index));
        }

        // دالة مساعدة
        private async Task PopulateDropdowns(object? selectedClient = null)
        {
            ViewBag.Clients = new SelectList(await _clientService.GetAllClientsAsync(), "Id", "Name", selectedClient);
            ViewBag.Materials = await _materialService.GetAllMaterialsAsync();
        }
    }
}