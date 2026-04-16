using FluentValidation;
using MaterialManagement.BLL.Features.Returns.Commands;
using MaterialManagement.BLL.ModelVM.Returns;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.Models;
using MaterialManagement.PL.Services;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.PL.Controllers
{
    [Route("sandbox/sales-returns")]
    public class SalesReturnSandboxController : Controller
    {
        private const string SandboxEnabledKey = "SalesReturnsSandbox:Enabled";
        private const string SandboxAccessKeyConfigKey = "SalesReturnsSandbox:AccessKey";
        private const string SandboxAccessKeyHeaderName = "X-Internal-Sandbox-Key";

        private readonly IMediator _mediator;
        private readonly MaterialManagementContext _context;
        private readonly ILogger<SalesReturnSandboxController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ISupervisorAuthorizationService _supervisorAuthorizationService;

        public SalesReturnSandboxController(
            IMediator mediator,
            MaterialManagementContext context,
            ILogger<SalesReturnSandboxController> logger,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ISupervisorAuthorizationService supervisorAuthorizationService)
        {
            _mediator = mediator;
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
            _supervisorAuthorizationService = supervisorAuthorizationService;
        }

        [HttpGet("create")]
        public async Task<IActionResult> Create(int? salesInvoiceId)
        {
            var gateResult = ValidateSandboxGate();
            if (gateResult != null)
            {
                return gateResult;
            }

            var viewModel = await BuildCreateViewModelAsync(salesInvoiceId, null);
            return View(viewModel);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesReturnCreateModel model, string? supervisorPassword)
        {
            var gateResult = ValidateSandboxGate();
            if (gateResult != null)
            {
                return gateResult;
            }

            if (!_supervisorAuthorizationService.TryAuthorize(supervisorPassword, out var supervisorError))
            {
                ModelState.AddModelError("SupervisorPassword", supervisorError);
                return View(await BuildCreateViewModelAsync(model.SalesInvoiceId, model));
            }

            model.Items = model.Items?
                .Where(i => i.ReturnedQuantity > 0)
                .ToList() ?? new List<SalesReturnItemCreateModel>();

            if (!model.Items.Any())
            {
                ModelState.AddModelError(string.Empty, "يجب إدخال كمية مرتجعة لبند واحد على الأقل.");
                return View(await BuildCreateViewModelAsync(model.SalesInvoiceId, model));
            }

            try
            {
                var result = await _mediator.Send(new CreateSalesReturnCommand { Model = model });
                TempData["SuccessMessage"] = $"تم إنشاء مرتجع البيع رقم {result.ReturnNumber} بنجاح.";
                return RedirectToAction(nameof(Create), new { salesInvoiceId = model.SalesInvoiceId });
            }
            catch (ValidationException ex)
            {
                foreach (var error in ex.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.ErrorMessage);
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "SalesReturn sandbox UI boundary caught concurrency collision for SalesInvoiceId {SalesInvoiceId}.",
                    model.SalesInvoiceId);

                ModelState.AddModelError(
                    string.Empty,
                    "تم تعديل الرصيد أو المخزون بواسطة مستخدم آخر. تم إلغاء مرتجع البيع بالكامل، يرجى المحاولة مرة أخرى.");
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while creating sandbox SalesReturn from Razor form for SalesInvoiceId {SalesInvoiceId}.",
                    model.SalesInvoiceId);

                ModelState.AddModelError(string.Empty, "حدث خطأ أثناء إنشاء مرتجع البيع.");
            }

            return View(await BuildCreateViewModelAsync(model.SalesInvoiceId, model));
        }

        [HttpPost("create-mediatr")]
        [Consumes("application/json")]
        public async Task<IActionResult> CreateMediatR([FromBody] SalesReturnCreateModel? model)
        {
            var gateResult = ValidateSandboxGate();
            if (gateResult != null)
            {
                return gateResult;
            }

            var supervisorPassword = Request.Headers["X-Supervisor-Password"].FirstOrDefault();
            if (!_supervisorAuthorizationService.TryAuthorize(supervisorPassword, out var supervisorError))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = supervisorError });
            }

            if (model == null)
            {
                return BadRequest(new { error = "بيانات مرتجع البيع مطلوبة." });
            }

            try
            {
                var result = await _mediator.Send(new CreateSalesReturnCommand { Model = model });
                return Ok(result);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new
                {
                    error = "بيانات مرتجع البيع غير صالحة.",
                    details = ex.Errors.Select(e => e.ErrorMessage).ToArray()
                });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "SalesReturn sandbox boundary caught concurrency collision for SalesInvoiceId {SalesInvoiceId}.",
                    model.SalesInvoiceId);

                return Conflict(new
                {
                    error = "تم تعديل الرصيد أو المخزون بواسطة مستخدم آخر. تم إلغاء مرتجع البيع بالكامل، يرجى المحاولة مرة أخرى."
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while creating sandbox SalesReturn for SalesInvoiceId {SalesInvoiceId}.",
                    model.SalesInvoiceId);

                return StatusCode(500, new { error = "حدث خطأ أثناء إنشاء مرتجع البيع." });
            }
        }

        private async Task<SalesReturnSandboxCreateViewModel> BuildCreateViewModelAsync(
            int? salesInvoiceId,
            SalesReturnCreateModel? submittedModel)
        {
            var viewModel = new SalesReturnSandboxCreateViewModel
            {
                SalesInvoiceId = salesInvoiceId,
                ReturnDate = submittedModel?.ReturnDate == default
                    ? DateTime.Now
                    : submittedModel?.ReturnDate ?? DateTime.Now,
                Notes = submittedModel?.Notes
            };

            if (!salesInvoiceId.HasValue || salesInvoiceId.Value <= 0)
            {
                return viewModel;
            }

            var invoice = await _context.SalesInvoices
                .IgnoreQueryFilters()
                .Include(i => i.Client)
                .Include(i => i.SalesInvoiceItems)
                    .ThenInclude(i => i.Material)
                .FirstOrDefaultAsync(i => i.Id == salesInvoiceId.Value);

            if (invoice == null)
            {
                ModelState.AddModelError(string.Empty, "فاتورة البيع غير موجودة.");
                return viewModel;
            }

            if (!invoice.IsActive)
            {
                ModelState.AddModelError(string.Empty, "لا يمكن إنشاء مرتجع بيع لفاتورة محذوفة أو غير نشطة.");
                return viewModel;
            }

            var invoiceItemIds = invoice.SalesInvoiceItems.Select(i => i.Id).ToList();
            var returnedQuantities = await _context.SalesReturnItems
                .IgnoreQueryFilters()
                .Where(ri =>
                    invoiceItemIds.Contains(ri.SalesInvoiceItemId) &&
                    ri.SalesReturn.IsActive &&
                    ri.SalesReturn.Status == ReturnStatus.Posted)
                .GroupBy(ri => ri.SalesInvoiceItemId)
                .Select(group => new
                {
                    SalesInvoiceItemId = group.Key,
                    ReturnedQuantity = group.Sum(item => item.ReturnedQuantity)
                })
                .ToDictionaryAsync(x => x.SalesInvoiceItemId, x => x.ReturnedQuantity);

            var submittedQuantities = submittedModel?.Items?
                .GroupBy(i => i.SalesInvoiceItemId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.ReturnedQuantity))
                ?? new Dictionary<int, decimal>();

            var discountRatio = invoice.TotalAmount > 0
                ? invoice.DiscountAmount / invoice.TotalAmount
                : 0m;

            viewModel.SalesInvoiceId = invoice.Id;
            viewModel.InvoiceNumber = invoice.InvoiceNumber;
            viewModel.ClientName = invoice.Client?.Name ?? "عميل غير متوفر";
            viewModel.InvoiceDate = invoice.InvoiceDate;
            viewModel.InvoiceGrossAmount = invoice.TotalAmount;
            viewModel.InvoiceDiscountAmount = invoice.DiscountAmount;
            viewModel.InvoiceNetAmount = invoice.TotalAmount - invoice.DiscountAmount;
            viewModel.Items = invoice.SalesInvoiceItems
                .OrderBy(item => item.Id)
                .Select(item =>
                {
                    returnedQuantities.TryGetValue(item.Id, out var alreadyReturned);
                    submittedQuantities.TryGetValue(item.Id, out var submittedQuantity);

                    return new SalesReturnSandboxInvoiceItemViewModel
                    {
                        SalesInvoiceItemId = item.Id,
                        MaterialCode = item.Material?.Code,
                        MaterialName = item.Material?.Name ?? "مادة غير متوفرة",
                        QuantitySold = item.Quantity,
                        QuantityAlreadyReturned = alreadyReturned,
                        QuantityRemaining = item.Quantity - alreadyReturned,
                        UnitPrice = item.UnitPrice,
                        NetUnitPrice = Math.Round(item.UnitPrice * (1 - discountRatio), 2, MidpointRounding.AwayFromZero),
                        ReturnedQuantity = submittedQuantity
                    };
                })
                .ToList();

            return viewModel;
        }

        private IActionResult? ValidateSandboxGate()
        {
            if (!IsSandboxEnabled())
            {
                return NotFound();
            }

            var configuredAccessKey = _configuration[SandboxAccessKeyConfigKey];
            if (_environment.IsProduction() && string.IsNullOrWhiteSpace(configuredAccessKey))
            {
                _logger.LogWarning(
                    "SalesReturn sandbox route was requested in Production, but {ConfigKey} is not configured.",
                    SandboxAccessKeyConfigKey);
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(configuredAccessKey) && !HasValidSandboxAccessKey(configuredAccessKey))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            return null;
        }

        private bool IsSandboxEnabled()
        {
            return bool.TryParse(_configuration[SandboxEnabledKey], out var isEnabled) && isEnabled;
        }

        private bool HasValidSandboxAccessKey(string configuredAccessKey)
        {
            if (!Request.Headers.TryGetValue(SandboxAccessKeyHeaderName, out var providedValues))
            {
                return false;
            }

            var providedAccessKey = providedValues.FirstOrDefault();
            if (string.IsNullOrEmpty(providedAccessKey))
            {
                return false;
            }

            return FixedTimeEquals(providedAccessKey, configuredAccessKey);
        }

        private static bool FixedTimeEquals(string providedValue, string expectedValue)
        {
            var providedBytes = Encoding.UTF8.GetBytes(providedValue);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedValue);

            return providedBytes.Length == expectedBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }
    }
}
