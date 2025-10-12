using MaterialManagement.BLL.ModelVM.Reservation;
using MaterialManagement.BLL.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        // GET: /Reservation (لعرض قائمة الحجوزات)
        public async Task<IActionResult> Index()
        {
            // 1. جلب كل الحجوزات النشطة فقط مع تفاصيلها
            var allReservations = await _reservationService.GetAllActiveReservationsWithDetailsAsync(); // سنقوم بإنشاء هذه الدالة

            // 2. تجميع الحجوزات حسب العميل باستخدام LINQ
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

        // GET: /Reservation/Details/5 (لعرض تفاصيل حجز)
        public async Task<IActionResult> Details(int id)
        {
            var model = await _reservationService.GetReservationDetailsAsync(id);
            if (model == null) return NotFound();
            return View(model);
        }

        // GET: /Reservation/Create (لفتح صفحة إنشاء حجز جديد)
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View();
        }

        // POST: /Reservation/Create (لحفظ الحجز الجديد)
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
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"حدث خطأ: {ex.Message}");
                }
            }

            if (model.Items.Count == 0)
            {
                ModelState.AddModelError("Items", "يجب إضافة صنف واحد على الأقل.");
            }

            await PopulateDropdowns(model.ClientId);
            return View(model);
        }

        // POST: /Reservation/Fulfill/5 (لتسليم الحجز وإنشاء فاتورة)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Fulfill(int id)
        {
            try
            {
                await _reservationService.FulfillReservationAsync(id);
                TempData["SuccessMessage"] = "تم تسليم الحجز وإنشاء الفاتورة بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"فشل تسليم الحجز: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: /Reservation/Cancel/5 (لإلغاء الحجز)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _reservationService.CancelReservationAsync(id);
                TempData["SuccessMessage"] = "تم إلغاء الحجز بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"فشل إلغاء الحجز: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        // دالة مساعدة لملء القوائم المنسدلة
        private async Task PopulateDropdowns(object? selectedClient = null)
        {
            ViewBag.Clients = new SelectList(await _clientService.GetAllClientsAsync(), "Id", "Name", selectedClient);
            ViewBag.Materials = await _materialService.GetAllMaterialsAsync();
        }
    }
}