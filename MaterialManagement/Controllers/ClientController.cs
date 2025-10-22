using MaterialManagement.BLL.ModelVM.Client;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using MaterialManagement.DAL.Repo.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System; 
using System.Collections.Generic; 
using System.Threading.Tasks;
using AutoMapper;
namespace MaterialManagement.PL.Controllers
{
    public class ClientController : Controller
    {
        private readonly IClientPaymentService _clientPaymentService;
        private readonly IClientService _clientService;
        private readonly IMapper _mapper;
        public ClientController(IClientService clientService, IClientPaymentService clientPaymentService, IMapper mapper)
        {
            _clientService = clientService;
            _clientPaymentService = clientPaymentService;
            _mapper = mapper;
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
            var payments = await _clientPaymentService.GetPaymentsForClientAsync(id); 
            ViewBag.Payments = payments;
            return View(client);
        }

        // GET: Client/Create
        public IActionResult Create()
        {
            return View();
        }

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

        [HttpPost]
        public async Task<IActionResult> LoadData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Request.Form["start"].FirstOrDefault();
            var length = Request.Form["length"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault();

            // فلتر مخصص للعملاء الذين عليهم مديونية
            var hasDebtFilter = Request.Form["hasDebtFilter"].FirstOrDefault();

            int pageSize = length != null ? Convert.ToInt32(length) : 10;
            int skip = start != null ? Convert.ToInt32(start) : 0;

            IQueryable<Client> query = _clientService.GetClientsAsQueryable();

            // تطبيق فلتر المديونية
            if (!string.IsNullOrEmpty(hasDebtFilter) && Convert.ToBoolean(hasDebtFilter))
            {
                query = query.Where(c => c.Balance > 0);
            }

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(c => c.Name.Contains(searchValue) || (c.Phone != null && c.Phone.Contains(searchValue)));
            }

            var recordsFiltered = await query.CountAsync();
            var pagedData = await query.Skip(skip).Take(pageSize).ToListAsync();
            var viewModelData = _mapper.Map<IEnumerable<ClientViewModel>>(pagedData);
            var recordsTotal = await _clientService.GetClientsAsQueryable().CountAsync();

            var jsonData = new { draw = draw, recordsFiltered = recordsFiltered, recordsTotal = recordsTotal, data = viewModelData };
            return Ok(jsonData);
        }
    }
}