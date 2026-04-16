using AutoMapper;
using MaterialManagement.BLL.Helper;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.ModelVM.Payment;
using MaterialManagement.BLL.Service.Implementations;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Enums;
using MaterialManagement.DAL.Repo.Implementations;
using MaterialManagement.PL.Controllers;
using MaterialManagement.PL.Models;
using MaterialManagement.PL.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IntegrationTestSandbox
{
    internal static partial class OneTimePartySmokeVerification
    {
        private const string ConnectionString = "Server=.;Database=MaterialManagementDB;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;";
        private const string SupervisorPassword = "1122335";

        private static readonly List<TestResult> Results = new();

        public static async Task<int> RunAsync()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var marker = $"OTP{DateTime.UtcNow:HHmmssfff}";
            Console.WriteLine($"Starting one-time party smoke verification. Marker={marker}");

            try
            {
                await CleanupAsync(marker);

                var registeredSales = await RunRegisteredSalesInvoiceAsync(marker);
                var walkInSales = await RunWalkInSalesInvoiceAsync(marker);
                var registeredPurchase = await RunRegisteredPurchaseInvoiceAsync(marker);
                var oneTimePurchase = await RunOneTimeSupplierPurchaseAsync(marker);
                await RunRegisteredClientReturnPurchaseAsync(marker);
                await RunPaymentAvailabilityGuardsAsync(walkInSales, oneTimePurchase, registeredSales.ClientId, registeredPurchase.SupplierId);
                await RunDisplayMappingChecksAsync(walkInSales.InvoiceId, oneTimePurchase.InvoiceId);
                await RunSupervisorDeleteChecksAsync(marker);
            }
            finally
            {
                await CleanupAsync(marker);
            }

            Console.WriteLine();
            Console.WriteLine("=== One-Time Party Smoke Verification Results ===");
            foreach (var result in Results)
            {
                Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} | {result.CaseName} | Expected: {result.Expected} | Actual: {result.Actual}");
            }

            var failedCount = Results.Count(result => !result.Passed);
            Console.WriteLine($"SUMMARY: {Results.Count - failedCount}/{Results.Count} checks passed.");
            return failedCount == 0 ? 0 : 1;
        }

        private static async Task<SalesSmokeResult> RunRegisteredSalesInvoiceAsync(string marker)
        {
            var clientId = await CreateClientAsync(marker, "registered-sales-client", 25m);
            var materialId = await CreateMaterialAsync(marker, "registered-sales-material", 100m, sellingPrice: 15m);
            var materialBefore = await GetMaterialQuantityAsync(materialId);
            var clientBalanceBefore = await GetClientBalanceAsync(clientId);

            SalesInvoiceViewModel created;
            await using (var context = CreateContext())
            {
                created = await CreateSalesService(context).CreateInvoiceAsync(new SalesInvoiceCreateModel
                {
                    PartyMode = SalesInvoicePartyMode.RegisteredClient,
                    ClientId = clientId,
                    PaidAmount = 10m,
                    DiscountAmount = 5m,
                    Notes = $"{marker}: registered sales",
                    Items = new List<SalesInvoiceItemCreateModel>
                    {
                        new() { MaterialId = materialId, Quantity = 2m, UnitPrice = 15m }
                    }
                });
            }

            var snapshot = await GetSalesInvoiceSnapshotAsync(created.Id);
            AddResult("1. Registered sales invoice - created", true, created.Id > 0);
            AddResult("1. Registered sales invoice - mode", SalesInvoicePartyMode.RegisteredClient, snapshot.PartyMode);
            AddResult("1. Registered sales invoice - client id", clientId, snapshot.ClientId);
            AddResult("1. Registered sales invoice - remaining", 15m, snapshot.RemainingAmount);
            AddResult("1. Registered sales invoice - stock decreased", materialBefore - 2m, await GetMaterialQuantityAsync(materialId));
            AddResult("1. Registered sales invoice - client balance", clientBalanceBefore + 15m, await GetClientBalanceAsync(clientId));

            return new SalesSmokeResult(created.Id, clientId, materialId);
        }

        private static async Task<SalesSmokeResult> RunWalkInSalesInvoiceAsync(string marker)
        {
            var materialId = await CreateMaterialAsync(marker, "walk-in-sales-material", 50m, sellingPrice: 20m);
            var materialBefore = await GetMaterialQuantityAsync(materialId);
            var expectedName = $"{marker} Cash Customer";

            SalesInvoiceViewModel created;
            await using (var context = CreateContext())
            {
                created = await CreateSalesService(context).CreateInvoiceAsync(new SalesInvoiceCreateModel
                {
                    PartyMode = SalesInvoicePartyMode.WalkInCustomer,
                    OneTimeCustomerName = expectedName,
                    OneTimeCustomerPhone = "0100000000",
                    PaidAmount = 20m,
                    Notes = $"{marker}: walk-in sales",
                    Items = new List<SalesInvoiceItemCreateModel>
                    {
                        new() { MaterialId = materialId, Quantity = 1m, UnitPrice = 20m }
                    }
                });
            }

            var snapshot = await GetSalesInvoiceSnapshotAsync(created.Id);
            AddResult("2. Walk-in sales - created", true, created.Id > 0);
            AddResult("2. Walk-in sales - mode", SalesInvoicePartyMode.WalkInCustomer, snapshot.PartyMode);
            AddResult<int?>("2. Walk-in sales - no registered client id", null, snapshot.ClientId);
            AddResult("2. Walk-in sales - display name persisted", expectedName, snapshot.OneTimeCustomerName);
            AddResult("2. Walk-in sales - remaining forced zero", 0m, snapshot.RemainingAmount);
            AddResult("2. Walk-in sales - stock decreased", materialBefore - 1m, await GetMaterialQuantityAsync(materialId));

            await using (var context = CreateContext())
            {
                var partialResult = await TryRunAsync(async () => await CreateSalesService(context).CreateInvoiceAsync(new SalesInvoiceCreateModel
                {
                    PartyMode = SalesInvoicePartyMode.WalkInCustomer,
                    OneTimeCustomerName = $"{marker} Partial Cash Customer",
                    PaidAmount = 10m,
                    Notes = $"{marker}: walk-in partial should fail",
                    Items = new List<SalesInvoiceItemCreateModel>
                    {
                        new() { MaterialId = materialId, Quantity = 1m, UnitPrice = 20m }
                    }
                }));

                AddResult("2. Walk-in sales - partial payment rejected", "failure", partialResult.Success ? "success" : "failure");
                AddResult("2. Walk-in sales - rejection mentions full settlement", true, partialResult.Error?.Contains("مسددة بالكامل") == true);
            }

            return new SalesSmokeResult(created.Id, null, materialId);
        }

        private static async Task<PurchaseSmokeResult> RunRegisteredPurchaseInvoiceAsync(string marker)
        {
            var supplierId = await CreateSupplierAsync(marker, "registered-purchase-supplier", 30m);
            var materialId = await CreateMaterialAsync(marker, "registered-purchase-material", 5m);
            var materialBefore = await GetMaterialQuantityAsync(materialId);
            var supplierBalanceBefore = await GetSupplierBalanceAsync(supplierId);

            PurchaseInvoiceViewModel created;
            await using (var context = CreateContext())
            {
                created = await CreatePurchaseService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
                {
                    PartyMode = PurchaseInvoicePartyMode.RegisteredSupplier,
                    SupplierId = supplierId,
                    PaidAmount = 10m,
                    DiscountAmount = 2m,
                    Notes = $"{marker}: registered purchase",
                    Items = new List<PurchaseInvoiceItemCreateModel>
                    {
                        new() { MaterialId = materialId, Quantity = 3m, UnitPrice = 7m }
                    }
                });
            }

            var snapshot = await GetPurchaseInvoiceSnapshotAsync(created.Id);
            AddResult("3. Registered purchase - created", true, created.Id > 0);
            AddResult("3. Registered purchase - mode", PurchaseInvoicePartyMode.RegisteredSupplier, snapshot.PartyMode);
            AddResult("3. Registered purchase - supplier id", supplierId, snapshot.SupplierId);
            AddResult("3. Registered purchase - remaining", 9m, snapshot.RemainingAmount);
            AddResult("3. Registered purchase - stock increased", materialBefore + 3m, await GetMaterialQuantityAsync(materialId));
            AddResult("3. Registered purchase - supplier balance", supplierBalanceBefore + 9m, await GetSupplierBalanceAsync(supplierId));

            return new PurchaseSmokeResult(created.Id, supplierId, materialId);
        }

        private static async Task<PurchaseSmokeResult> RunOneTimeSupplierPurchaseAsync(string marker)
        {
            var materialId = await CreateMaterialAsync(marker, "one-time-supplier-material", 8m);
            var materialBefore = await GetMaterialQuantityAsync(materialId);
            var expectedName = $"{marker} Manual Supplier";

            PurchaseInvoiceViewModel created;
            await using (var context = CreateContext())
            {
                created = await CreatePurchaseService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
                {
                    PartyMode = PurchaseInvoicePartyMode.OneTimeSupplier,
                    OneTimeSupplierName = expectedName,
                    OneTimeSupplierPhone = "0111111111",
                    PaidAmount = 16m,
                    Notes = $"{marker}: one-time supplier purchase",
                    Items = new List<PurchaseInvoiceItemCreateModel>
                    {
                        new() { MaterialId = materialId, Quantity = 2m, UnitPrice = 8m }
                    }
                });
            }

            var snapshot = await GetPurchaseInvoiceSnapshotAsync(created.Id);
            AddResult("4. One-time supplier purchase - created", true, created.Id > 0);
            AddResult("4. One-time supplier purchase - mode", PurchaseInvoicePartyMode.OneTimeSupplier, snapshot.PartyMode);
            AddResult<int?>("4. One-time supplier purchase - no supplier id", null, snapshot.SupplierId);
            AddResult<int?>("4. One-time supplier purchase - no client id", null, snapshot.ClientId);
            AddResult("4. One-time supplier purchase - display name persisted", expectedName, snapshot.OneTimeSupplierName);
            AddResult("4. One-time supplier purchase - remaining forced zero", 0m, snapshot.RemainingAmount);
            AddResult("4. One-time supplier purchase - stock increased", materialBefore + 2m, await GetMaterialQuantityAsync(materialId));

            await using (var context = CreateContext())
            {
                var partialResult = await TryRunAsync(async () => await CreatePurchaseService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
                {
                    PartyMode = PurchaseInvoicePartyMode.OneTimeSupplier,
                    OneTimeSupplierName = $"{marker} Partial Manual Supplier",
                    PaidAmount = 10m,
                    Notes = $"{marker}: one-time supplier partial should fail",
                    Items = new List<PurchaseInvoiceItemCreateModel>
                    {
                        new() { MaterialId = materialId, Quantity = 2m, UnitPrice = 8m }
                    }
                }));

                AddResult("4. One-time supplier purchase - partial payment rejected", "failure", partialResult.Success ? "success" : "failure");
                AddResult("4. One-time supplier purchase - rejection mentions full settlement", true, partialResult.Error?.Contains("مسددة بالكامل") == true);
            }

            return new PurchaseSmokeResult(created.Id, null, materialId);
        }

        private static async Task RunRegisteredClientReturnPurchaseAsync(string marker)
        {
            var clientId = await CreateClientAsync(marker, "client-return-client", 100m);
            var materialId = await CreateMaterialAsync(marker, "client-return-material", 4m, purchasePrice: 3m);
            var materialBefore = await GetMaterialQuantityAsync(materialId);
            var purchasePriceBefore = await GetMaterialPurchasePriceAsync(materialId);
            var clientBalanceBefore = await GetClientBalanceAsync(clientId);

            PurchaseInvoiceViewModel created;
            await using (var context = CreateContext())
            {
                created = await CreatePurchaseService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
                {
                    PartyMode = PurchaseInvoicePartyMode.RegisteredClientReturn,
                    ClientId = clientId,
                    PaidAmount = 4m,
                    Notes = $"{marker}: registered client return",
                    Items = new List<PurchaseInvoiceItemCreateModel>
                    {
                        new() { MaterialId = materialId, Quantity = 2m, UnitPrice = 11m }
                    }
                });
            }

            var snapshot = await GetPurchaseInvoiceSnapshotAsync(created.Id);
            AddResult("5. Registered client-return purchase - created", true, created.Id > 0);
            AddResult("5. Registered client-return purchase - mode", PurchaseInvoicePartyMode.RegisteredClientReturn, snapshot.PartyMode);
            AddResult<int?>("5. Registered client-return purchase - no supplier id", null, snapshot.SupplierId);
            AddResult("5. Registered client-return purchase - client id", clientId, snapshot.ClientId);
            AddResult("5. Registered client-return purchase - stock increased", materialBefore + 2m, await GetMaterialQuantityAsync(materialId));
            AddResult("5. Registered client-return purchase - purchase price unchanged", purchasePriceBefore, await GetMaterialPurchasePriceAsync(materialId));
            AddResult("5. Registered client-return purchase - client balance reduced by remaining", clientBalanceBefore - 18m, await GetClientBalanceAsync(clientId));
        }

        private static async Task RunPaymentAvailabilityGuardsAsync(
            SalesSmokeResult walkInSales,
            PurchaseSmokeResult oneTimePurchase,
            int? registeredClientId,
            int? registeredSupplierId)
        {
            await using (var context = CreateContext())
            {
                var unpaidWalkIn = await CreateSalesService(context).GetUnpaidInvoicesForClientAsync(0);
                AddResult("6. Payment flow - walk-in invoices absent from client unpaid list", false, unpaidWalkIn.Any(i => i.Id == walkInSales.InvoiceId));
            }

            await using (var context = CreateContext())
            {
                var unpaidOneTime = await CreatePurchaseService(context).GetUnpaidInvoicesForSupplierAsync(0);
                AddResult("6. Payment flow - one-time supplier invoices absent from supplier unpaid list", false, unpaidOneTime.Any(i => i.Id == oneTimePurchase.InvoiceId));
            }

            if (registeredClientId.HasValue)
            {
                await using var context = CreateContext();
                var attempt = await TryRunAsync(async () => await CreateClientPaymentService(context).AddPaymentAsync(new ClientPaymentCreateModel
                {
                    ClientId = registeredClientId.Value,
                    SalesInvoiceId = walkInSales.InvoiceId,
                    Amount = 1m,
                    PaymentMethod = "cash",
                    Notes = "one-time smoke should fail"
                }));
                AddResult("6. Payment flow - direct client payment against walk-in invoice rejected", "failure", attempt.Success ? "success" : "failure");
            }

            if (registeredSupplierId.HasValue)
            {
                await using var context = CreateContext();
                var attempt = await TryRunAsync(async () => await CreateSupplierPaymentService(context).AddPaymentAsync(new SupplierPaymentCreateModel
                {
                    SupplierId = registeredSupplierId.Value,
                    PurchaseInvoiceId = oneTimePurchase.InvoiceId,
                    Amount = 1m,
                    PaymentMethod = "cash",
                    Notes = "one-time smoke should fail"
                }));
                AddResult("6. Payment flow - direct supplier payment against one-time invoice rejected", "failure", attempt.Success ? "success" : "failure");
            }

            var salesDetailsView = await File.ReadAllTextAsync(Path.Combine("MaterialManagement", "Views", "SalesInvoice", "Details.cshtml"));
            var purchaseDetailsView = await File.ReadAllTextAsync(Path.Combine("MaterialManagement", "Views", "PurchaseInvoice", "Details.cshtml"));
            AddResult("6. Payment flow - sales details button guarded by registered client id", true, salesDetailsView.Contains("Model.ClientId.HasValue"));
            AddResult("6. Payment flow - purchase details payment section registered-supplier only", true, purchaseDetailsView.Contains("isRegisteredSupplierPurchase"));
        }

        private static async Task RunDisplayMappingChecksAsync(int walkInInvoiceId, int oneTimePurchaseId)
        {
            await using (var context = CreateContext())
            {
                var walkInDetails = await CreateSalesService(context).GetInvoiceByIdAsync(walkInInvoiceId);
                AddResult("7. Display - walk-in details client name uses one-time name", walkInDetails?.OneTimeCustomerName, walkInDetails?.ClientName);

                var clientSummaries = await CreateSalesService(context).GetClientInvoiceSummariesAsync();
                AddResult("7. Display - sales index has cash customer summary row", true, clientSummaries.Any(s => s.ClientId == 0 && s.ClientName.Contains("نقديون")));
            }

            await using (var context = CreateContext())
            {
                var oneTimeDetails = await CreatePurchaseService(context).GetInvoiceByIdAsync(oneTimePurchaseId);
                AddResult("7. Display - one-time purchase details supplier name uses manual name", oneTimeDetails?.OneTimeSupplierName, oneTimeDetails?.SupplierName);

                var supplierSummaries = await CreatePurchaseService(context).GetSupplierInvoiceSummariesAsync();
                AddResult("7. Display - purchase index has manual supplier summary row", true, supplierSummaries.Any(s => s.SupplierId == 0 && s.SupplierName.Contains("يدويون")));
            }
        }

        private static async Task RunSupervisorDeleteChecksAsync(string marker)
        {
            var wrongDeleteClientId = await CreateClientAsync(marker, "wrong-delete-client", 0m);
            var wrongDeleteMaterialId = await CreateMaterialAsync(marker, "wrong-delete-material", 20m, sellingPrice: 5m);
            int wrongDeleteInvoiceId;
            await using (var context = CreateContext())
            {
                var created = await CreateSalesService(context).CreateInvoiceAsync(new SalesInvoiceCreateModel
                {
                    PartyMode = SalesInvoicePartyMode.RegisteredClient,
                    ClientId = wrongDeleteClientId,
                    PaidAmount = 5m,
                    Notes = $"{marker}: wrong supervisor delete should fail",
                    Items = new List<SalesInvoiceItemCreateModel>
                    {
                        new() { MaterialId = wrongDeleteMaterialId, Quantity = 1m, UnitPrice = 5m }
                    }
                });
                wrongDeleteInvoiceId = created.Id;
            }

            await using (var context = CreateContext())
            {
                var controller = CreateSalesInvoiceController(context);
                var actionResult = await controller.DeleteConfirmed(wrongDeleteInvoiceId, "bad-password");
                AddResult("9. Supervisor delete - wrong password returns same view", nameof(ViewResult), actionResult.GetType().Name);
                AddResult("9. Supervisor delete - wrong password adds model error", true, controller.ModelState.ContainsKey("SupervisorPassword"));
                AddResult("9. Supervisor delete - wrong password leaves invoice active", true, await IsSalesInvoiceActiveAsync(wrongDeleteInvoiceId));
            }

            var correctDeleteClientId = await CreateClientAsync(marker, "correct-delete-client", 0m);
            var correctDeleteMaterialId = await CreateMaterialAsync(marker, "correct-delete-material", 20m, sellingPrice: 6m);
            int correctDeleteInvoiceId;
            await using (var context = CreateContext())
            {
                var created = await CreateSalesService(context).CreateInvoiceAsync(new SalesInvoiceCreateModel
                {
                    PartyMode = SalesInvoicePartyMode.RegisteredClient,
                    ClientId = correctDeleteClientId,
                    PaidAmount = 6m,
                    Notes = $"{marker}: correct supervisor sales delete",
                    Items = new List<SalesInvoiceItemCreateModel>
                    {
                        new() { MaterialId = correctDeleteMaterialId, Quantity = 1m, UnitPrice = 6m }
                    }
                });
                correctDeleteInvoiceId = created.Id;
            }

            await using (var context = CreateContext())
            {
                var controller = CreateSalesInvoiceController(context);
                var actionResult = await controller.DeleteConfirmed(correctDeleteInvoiceId, SupervisorPassword);
                AddResult("8. Supervisor delete - correct sales password redirects", nameof(RedirectToActionResult), actionResult.GetType().Name);
                AddResult("8. Supervisor delete - correct sales password soft-deletes invoice", false, await IsSalesInvoiceActiveAsync(correctDeleteInvoiceId));
            }

            var purchaseDeleteSupplierId = await CreateSupplierAsync(marker, "correct-delete-supplier", 0m);
            var purchaseDeleteMaterialId = await CreateMaterialAsync(marker, "purchase-delete-material", 1m);
            int purchaseDeleteInvoiceId;
            await using (var context = CreateContext())
            {
                var created = await CreatePurchaseService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
                {
                    PartyMode = PurchaseInvoicePartyMode.RegisteredSupplier,
                    SupplierId = purchaseDeleteSupplierId,
                    PaidAmount = 4m,
                    Notes = $"{marker}: correct supervisor purchase delete",
                    Items = new List<PurchaseInvoiceItemCreateModel>
                    {
                        new() { MaterialId = purchaseDeleteMaterialId, Quantity = 1m, UnitPrice = 4m }
                    }
                });
                purchaseDeleteInvoiceId = created.Id;
            }

            await using (var context = CreateContext())
            {
                var controller = CreatePurchaseInvoiceController(context);
                var actionResult = await controller.DeleteConfirmed(purchaseDeleteInvoiceId, SupervisorPassword);
                AddResult("8. Supervisor delete - correct purchase password redirects", nameof(RedirectToActionResult), actionResult.GetType().Name);
                AddResult("8. Supervisor delete - correct purchase password soft-deletes invoice", false, await IsPurchaseInvoiceActiveAsync(purchaseDeleteInvoiceId));
            }
        }

        private static SalesInvoiceService CreateSalesService(MaterialManagementContext context)
        {
            return new SalesInvoiceService(
                new SalesInvoiceRepo(context),
                new MaterialRepo(context),
                new ClientRepo(context),
                context,
                CreateMapper());
        }

        private static PurchaseInvoiceService CreatePurchaseService(MaterialManagementContext context)
        {
            return new PurchaseInvoiceService(
                new PurchaseInvoiceRepo(context),
                new MaterialRepo(context),
                new SupplierRepo(context),
                new ClientRepo(context),
                context,
                CreateMapper());
        }

        private static ClientPaymentService CreateClientPaymentService(MaterialManagementContext context)
        {
            return new ClientPaymentService(
                new ClientPaymentRepo(context),
                new ClientRepo(context),
                new SalesInvoiceRepo(context),
                context,
                CreateMapper());
        }

        private static SupplierPaymentService CreateSupplierPaymentService(MaterialManagementContext context)
        {
            return new SupplierPaymentService(
                new SupplierPaymentRepo(context),
                context,
                CreateMapper());
        }

        private static SalesInvoiceController CreateSalesInvoiceController(MaterialManagementContext context)
        {
            var controller = new SalesInvoiceController(
                CreateSalesService(context),
                null!,
                null!,
                null!,
                CreateMapper(),
                null!,
                NullLogger<SalesInvoiceController>.Instance,
                CreateSupervisorAuthorizationService());
            AttachControllerContext(controller);
            return controller;
        }

        private static PurchaseInvoiceController CreatePurchaseInvoiceController(MaterialManagementContext context)
        {
            var controller = new PurchaseInvoiceController(
                CreatePurchaseService(context),
                null!,
                null!,
                null!,
                null!,
                CreateMapper(),
                CreateSupervisorAuthorizationService());
            AttachControllerContext(controller);
            return controller;
        }

        private static SupervisorAuthorizationService CreateSupervisorAuthorizationService()
        {
            return new SupervisorAuthorizationService(Options.Create(new SupervisorAuthorizationOptions
            {
                Password = SupervisorPassword
            }));
        }

        private static void AttachControllerContext(Controller controller)
        {
            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        }

        private static IMapper CreateMapper()
        {
            return new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfile>()).CreateMapper();
        }

        private static MaterialManagementContext CreateContext()
        {
            return new MaterialManagementContext(
                new DbContextOptionsBuilder<MaterialManagementContext>()
                    .UseSqlServer(ConnectionString)
                    .EnableSensitiveDataLogging()
                    .Options);
        }

        private static async Task<int> CreateClientAsync(string marker, string suffix, decimal balance)
        {
            await using var context = CreateContext();
            var client = new Client
            {
                Name = $"{marker}-{suffix}",
                Phone = CreatePhone(marker, suffix),
                Address = marker,
                Balance = balance,
                IsActive = true
            };
            context.Clients.Add(client);
            await context.SaveChangesAsync();
            return client.Id;
        }

        private static async Task<int> CreateSupplierAsync(string marker, string suffix, decimal balance)
        {
            await using var context = CreateContext();
            var supplier = new Supplier
            {
                Name = $"{marker}-{suffix}",
                Phone = CreatePhone(marker, suffix),
                Address = marker,
                Balance = balance,
                IsActive = true
            };
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            return supplier.Id;
        }

        private static async Task<int> CreateMaterialAsync(
            string marker,
            string suffix,
            decimal quantity,
            decimal? sellingPrice = null,
            decimal? purchasePrice = null)
        {
            await using var context = CreateContext();
            var material = new Material
            {
                Name = $"{marker}-{suffix}",
                Code = $"{marker}-{suffix}",
                Unit = "pcs",
                Quantity = quantity,
                ReservedQuantity = 0m,
                SellingPrice = sellingPrice,
                PurchasePrice = purchasePrice,
                Description = marker,
                IsActive = true
            };
            context.Materials.Add(material);
            await context.SaveChangesAsync();
            return material.Id;
        }

        private static async Task<SalesInvoiceSnapshot> GetSalesInvoiceSnapshotAsync(int invoiceId)
        {
            await using var context = CreateContext();
            var invoice = await context.SalesInvoices
                .IgnoreQueryFilters()
                .SingleAsync(i => i.Id == invoiceId);

            return new SalesInvoiceSnapshot(
                invoice.PartyMode,
                invoice.ClientId,
                invoice.OneTimeCustomerName,
                invoice.RemainingAmount);
        }

        private static async Task<PurchaseInvoiceSnapshot> GetPurchaseInvoiceSnapshotAsync(int invoiceId)
        {
            await using var context = CreateContext();
            var invoice = await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .SingleAsync(i => i.Id == invoiceId);

            return new PurchaseInvoiceSnapshot(
                invoice.PartyMode,
                invoice.SupplierId,
                invoice.ClientId,
                invoice.OneTimeSupplierName,
                invoice.RemainingAmount);
        }

        private static async Task<decimal> GetMaterialQuantityAsync(int materialId)
        {
            await using var context = CreateContext();
            return await context.Materials
                .IgnoreQueryFilters()
                .Where(m => m.Id == materialId)
                .Select(m => m.Quantity)
                .SingleAsync();
        }

        private static async Task<decimal?> GetMaterialPurchasePriceAsync(int materialId)
        {
            await using var context = CreateContext();
            return await context.Materials
                .IgnoreQueryFilters()
                .Where(m => m.Id == materialId)
                .Select(m => m.PurchasePrice)
                .SingleAsync();
        }

        private static async Task<decimal> GetClientBalanceAsync(int clientId)
        {
            await using var context = CreateContext();
            return await context.Clients
                .IgnoreQueryFilters()
                .Where(c => c.Id == clientId)
                .Select(c => c.Balance)
                .SingleAsync();
        }

        private static async Task<decimal> GetSupplierBalanceAsync(int supplierId)
        {
            await using var context = CreateContext();
            return await context.Suppliers
                .IgnoreQueryFilters()
                .Where(s => s.Id == supplierId)
                .Select(s => s.Balance)
                .SingleAsync();
        }

        private static async Task<bool> IsSalesInvoiceActiveAsync(int invoiceId)
        {
            await using var context = CreateContext();
            return await context.SalesInvoices
                .IgnoreQueryFilters()
                .Where(i => i.Id == invoiceId)
                .Select(i => i.IsActive)
                .SingleAsync();
        }

        private static async Task<bool> IsPurchaseInvoiceActiveAsync(int invoiceId)
        {
            await using var context = CreateContext();
            return await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(i => i.Id == invoiceId)
                .Select(i => i.IsActive)
                .SingleAsync();
        }

        private static async Task CleanupAsync(string marker)
        {
            await using var context = CreateContext();

            var clientIds = await context.Clients
                .IgnoreQueryFilters()
                .Where(c => c.Name.Contains(marker) || c.Address == marker)
                .Select(c => c.Id)
                .ToListAsync();

            var supplierIds = await context.Suppliers
                .IgnoreQueryFilters()
                .Where(s => s.Name.Contains(marker) || s.Address == marker)
                .Select(s => s.Id)
                .ToListAsync();

            var materialIds = await context.Materials
                .IgnoreQueryFilters()
                .Where(m => m.Code.Contains(marker) || m.Description == marker)
                .Select(m => m.Id)
                .ToListAsync();

            var salesInvoiceIds = await context.SalesInvoices
                .IgnoreQueryFilters()
                .Where(i =>
                    i.InvoiceNumber.Contains(marker) ||
                    (i.Notes != null && i.Notes.Contains(marker)) ||
                    (i.OneTimeCustomerName != null && i.OneTimeCustomerName.Contains(marker)) ||
                    (i.ClientId.HasValue && clientIds.Contains(i.ClientId.Value)) ||
                    i.SalesInvoiceItems.Any(item => materialIds.Contains(item.MaterialId)))
                .Select(i => i.Id)
                .ToListAsync();

            var purchaseInvoiceIds = await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(i =>
                    i.InvoiceNumber.Contains(marker) ||
                    (i.Notes != null && i.Notes.Contains(marker)) ||
                    (i.OneTimeSupplierName != null && i.OneTimeSupplierName.Contains(marker)) ||
                    (i.SupplierId.HasValue && supplierIds.Contains(i.SupplierId.Value)) ||
                    (i.ClientId.HasValue && clientIds.Contains(i.ClientId.Value)) ||
                    i.PurchaseInvoiceItems.Any(item => materialIds.Contains(item.MaterialId)))
                .Select(i => i.Id)
                .ToListAsync();

            var salesReturnIds = await context.SalesReturns
                .IgnoreQueryFilters()
                .Where(r => salesInvoiceIds.Contains(r.SalesInvoiceId))
                .Select(r => r.Id)
                .ToListAsync();

            if (salesInvoiceIds.Count > 0 || clientIds.Count > 0)
            {
                await context.ClientPayments
                    .IgnoreQueryFilters()
                    .Where(p =>
                        clientIds.Contains(p.ClientId) ||
                        (p.SalesInvoiceId.HasValue && salesInvoiceIds.Contains(p.SalesInvoiceId.Value)))
                    .ExecuteDeleteAsync();
            }

            if (purchaseInvoiceIds.Count > 0 || supplierIds.Count > 0)
            {
                await context.SupplierPayments
                    .IgnoreQueryFilters()
                    .Where(p =>
                        supplierIds.Contains(p.SupplierId) ||
                        (p.PurchaseInvoiceId.HasValue && purchaseInvoiceIds.Contains(p.PurchaseInvoiceId.Value)))
                    .ExecuteDeleteAsync();
            }

            if (salesReturnIds.Count > 0)
            {
                await context.SalesReturnItems
                    .IgnoreQueryFilters()
                    .Where(item => salesReturnIds.Contains(item.SalesReturnId))
                    .ExecuteDeleteAsync();

                await context.SalesReturns
                    .IgnoreQueryFilters()
                    .Where(r => salesReturnIds.Contains(r.Id))
                    .ExecuteDeleteAsync();
            }

            if (salesInvoiceIds.Count > 0)
            {
                await context.SalesInvoiceItems
                    .IgnoreQueryFilters()
                    .Where(item => salesInvoiceIds.Contains(item.SalesInvoiceId))
                    .ExecuteDeleteAsync();

                await context.SalesInvoices
                    .IgnoreQueryFilters()
                    .Where(i => salesInvoiceIds.Contains(i.Id))
                    .ExecuteDeleteAsync();
            }

            if (purchaseInvoiceIds.Count > 0)
            {
                await context.PurchaseInvoiceItems
                    .IgnoreQueryFilters()
                    .Where(item => purchaseInvoiceIds.Contains(item.PurchaseInvoiceId))
                    .ExecuteDeleteAsync();

                await context.PurchaseInvoices
                    .IgnoreQueryFilters()
                    .Where(i => purchaseInvoiceIds.Contains(i.Id))
                    .ExecuteDeleteAsync();
            }

            if (clientIds.Count > 0)
            {
                await context.Clients
                    .IgnoreQueryFilters()
                    .Where(c => clientIds.Contains(c.Id))
                    .ExecuteDeleteAsync();
            }

            if (supplierIds.Count > 0)
            {
                await context.Suppliers
                    .IgnoreQueryFilters()
                    .Where(s => supplierIds.Contains(s.Id))
                    .ExecuteDeleteAsync();
            }

            if (materialIds.Count > 0)
            {
                await context.Materials
                    .IgnoreQueryFilters()
                    .Where(m => materialIds.Contains(m.Id))
                    .ExecuteDeleteAsync();
            }
        }

        private static async Task<CommandAttempt> TryRunAsync(Func<Task<object?>> action)
        {
            try
            {
                await action();
                return new CommandAttempt(true, null);
            }
            catch (Exception ex)
            {
                return new CommandAttempt(false, ex.Message);
            }
        }

        private static string CreatePhone(string marker, string suffix)
        {
            var hash = Math.Abs(HashCode.Combine(marker, suffix)).ToString();
            return $"8{hash}".PadRight(15, '0')[..15];
        }

        private static void AddResult<T>(string caseName, T expected, T actual)
        {
            Results.Add(new TestResult(
                caseName,
                expected?.ToString() ?? "null",
                actual?.ToString() ?? "null",
                EqualityComparer<T>.Default.Equals(expected, actual)));
        }

        private sealed class NoOpTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

            public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            {
            }
        }

        private sealed record TestResult(string CaseName, string Expected, string Actual, bool Passed);

        private sealed record CommandAttempt(bool Success, string? Error);

        private sealed record SalesSmokeResult(int InvoiceId, int? ClientId, int MaterialId);

        private sealed record PurchaseSmokeResult(int InvoiceId, int? SupplierId, int MaterialId);

        private sealed record SalesInvoiceSnapshot(
            SalesInvoicePartyMode PartyMode,
            int? ClientId,
            string? OneTimeCustomerName,
            decimal RemainingAmount);

        private sealed record PurchaseInvoiceSnapshot(
            PurchaseInvoicePartyMode PartyMode,
            int? SupplierId,
            int? ClientId,
            string? OneTimeSupplierName,
            decimal RemainingAmount);
    }
}
