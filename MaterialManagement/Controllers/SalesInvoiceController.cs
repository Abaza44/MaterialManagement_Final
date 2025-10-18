using AutoMapper;
using MaterialManagement.BLL.ModelVM.Client;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions; // <-- مهم
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace MaterialManagement.PL.Controllers
{
    public class SalesInvoiceController : Controller
    {
        private readonly ISalesInvoiceService _salesInvoiceService;
        private readonly IClientService _clientService;
        private readonly IMaterialService _materialService;
        private readonly IClientPaymentRepo _clientPaymentRepo; // <<< تم إضافته هنا
        private readonly IMapper _mapper; 
        // <<< تم تحديث الـ Constructor >>>
        public SalesInvoiceController(
            ISalesInvoiceService salesInvoiceService,
            IClientService clientService,
            IMaterialService materialService,
            IClientPaymentRepo clientPaymentRepo,
            IMapper mapper)
        {
            _salesInvoiceService = salesInvoiceService;
            _clientService = clientService;
            _materialService = materialService;
            _clientPaymentRepo = clientPaymentRepo; 
            _mapper = mapper;
        }

        // GET: SalesInvoice
        // في SalesInvoiceController.cs
        public async Task<IActionResult> Index()
        {
            var clientSummaries = await _salesInvoiceService.GetClientInvoiceSummariesAsync();
            return View(clientSummaries);
        }

        // GET: SalesInvoice/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _salesInvoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "الفاتورة غير موجودة";
                return RedirectToAction(nameof(Index));
            }

            // <<< جلب سجل الدفعات المرتبط بهذه الفاتورة >>>
            var payments = await _clientPaymentRepo.GetByInvoiceIdAsync(id);
            ViewBag.Payments = payments;

            return View(invoice);
        }

        // GET: SalesInvoice/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Clients = await _clientService.GetAllClientsAsync();
            ViewBag.Materials = await _materialService.GetAllMaterialsAsync();

            var model = new SalesInvoiceCreateModel
            {
                Items = new List<SalesInvoiceItemCreateModel> { new SalesInvoiceItemCreateModel() }
            };

            return View(model);
        }

        // POST: SalesInvoice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesInvoiceCreateModel model)
        {
            // إزالة البنود الفارغة التي قد يرسلها الفورم
            model.Items.RemoveAll(i => i.Quantity == 0 || i.UnitPrice == 0);

            if (!ModelState.IsValid || !model.Items.Any())
            {
                if (!model.Items.Any())
                {
                    ModelState.AddModelError("Items", "يجب إضافة بند واحد على الأقل للفاتورة.");
                }
                ViewBag.Clients = await _clientService.GetAllClientsAsync();
                ViewBag.Materials = await _materialService.GetAllMaterialsAsync();
                return View(model);
            }

            try
            {
                await _salesInvoiceService.CreateInvoiceAsync(model);
                TempData["SuccessMessage"] = "تم إنشاء فاتورة بيع بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                ViewBag.Clients = await _clientService.GetAllClientsAsync();
                ViewBag.Materials = await _materialService.GetAllMaterialsAsync();
                return View(model);
            }
        }

        // GET: SalesInvoice/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var invoice = await _salesInvoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "الفاتورة غير موجودة";
                return RedirectToAction(nameof(Index));
            }
            return View(invoice);
        }

        // POST: SalesInvoice/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _salesInvoiceService.DeleteInvoiceAsync(id);
                TempData["SuccessMessage"] = "تم حذف الفاتورة بنجاح";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
 
        public async Task<IActionResult> ClientInvoices(int id)
        {
            var client = await _clientService.GetClientByIdAsync(id);
            if (client == null) return NotFound();

            return View(new ClientViewModel { Id = client.Id, Name = client.Name });
        }

        [HttpPost]
        public async Task<IActionResult> LoadClientInvoices()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Request.Form["start"].FirstOrDefault();
            var length = Request.Form["length"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault();
            var clientId = int.Parse(Request.Form["clientId"].FirstOrDefault());

            int pageSize = length != null ? Convert.ToInt32(length) : 10;
            int skip = start != null ? Convert.ToInt32(start) : 0;

            IQueryable<SalesInvoice> query = _salesInvoiceService.GetInvoicesAsQueryable()
                                               .Where(i => i.ClientId == clientId && i.IsActive);

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(i => i.InvoiceNumber.Contains(searchValue));
            }

            var recordsFiltered = await query.CountAsync();
            var pagedData = await query.OrderByDescending(i => i.InvoiceDate).Skip(skip).Take(pageSize).ToListAsync();
            var viewModelData = _mapper.Map<IEnumerable<InvoiceSummaryViewModel>>(pagedData);
            var recordsTotal = await _salesInvoiceService.GetInvoicesAsQueryable().Where(i => i.ClientId == clientId && i.IsActive).CountAsync();

            var jsonData = new { draw = draw, recordsFiltered = recordsFiltered, recordsTotal = recordsTotal, data = viewModelData };
            return Ok(jsonData);
        }
    }
}