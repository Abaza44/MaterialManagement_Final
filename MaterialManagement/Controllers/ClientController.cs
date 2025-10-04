using MaterialManagement.BLL.ModelVM.Client;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Repo.Abstractions;
using MaterialManagement.DAL.Repo.Implementations;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic; // <-- أضف هذا
using System.Threading.Tasks; // <-- أضف هذا

using System; // <-- أضف هذا
namespace MaterialManagement.PL.Controllers
{
    public class ClientController : Controller
    {
        private readonly IClientPaymentRepo _clientPaymentRepo;
        private readonly IClientService _clientService;

        public ClientController(IClientService clientService,IClientPaymentRepo clientPaymentRepo)
        {
            _clientService = clientService;
            _clientPaymentRepo = clientPaymentRepo;
        }

        // GET: Client
        public async Task<IActionResult> Index(string searchTerm)
        {
            IEnumerable<ClientViewModel> clients;

            if (!string.IsNullOrEmpty(searchTerm))
            {
                clients = await _clientService.SearchClientsAsync(searchTerm);
                ViewBag.SearchTerm = searchTerm;
            }
            else
            {
                clients = await _clientService.GetAllClientsAsync();
            }

            return View(clients);
        }

        // GET: Client/Details/5
        public async Task<IActionResult> Details(int id)
        {

            var client = await _clientService.GetClientByIdAsync(id);
            if (client == null)
            {
                TempData["ErrorMessage"] = "العميل غير موجود";
                return RedirectToAction(nameof(Index));
            }

            // <<< الآن هذا الكود سيعمل بشكل صحيح >>>
            var payments = await _clientPaymentRepo.GetByClientIdAsync(id);
            ViewBag.Payments = payments;

            return View(client);
        }

        // GET: Client/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Client/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClientCreateModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                await _clientService.CreateClientAsync(model);
                TempData["SuccessMessage"] = "✅ تم إضافة العميل بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;  // 👈 هنا هتظهر رسالة "رقم الهاتف موجود بالفعل"
                return View(model);
            }
        }

        // GET: Client/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var client = await _clientService.GetClientByIdAsync(id);
            if (client == null)
            {
                TempData["ErrorMessage"] = "العميل غير موجود";
                return RedirectToAction(nameof(Index));
            }

            var model = new ClientUpdateModel
            {
                Name = client.Name,
                Phone = client.Phone,
                Address = client.Address,
                Balance = client.Balance,
                IsActive = client.IsActive
            };
            return View(model);
        }

        // POST: Client/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ClientUpdateModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                await _clientService.UpdateClientAsync(id, model);
                TempData["SuccessMessage"] = "تم تعديل العميل بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return View(model);
            }
        }

        // GET: Client/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var client = await _clientService.GetClientByIdAsync(id);
            if (client == null)
            {
                TempData["ErrorMessage"] = "العميل غير موجود";
                return RedirectToAction(nameof(Index));
            }
            return View(client);
        }

        // POST: Client/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _clientService.DeleteClientAsync(id);
                TempData["SuccessMessage"] = "✅ تم حذف العميل بنجاح";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;   // 👈 هنا الرسالة بتتخزن
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Reactivate(int id)
        {
            try
            {
                await _clientService.ReactivateClientAsync(id);
                TempData["SuccessMessage"] = "تم إعادة تفعيل العميل بنجاح";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}