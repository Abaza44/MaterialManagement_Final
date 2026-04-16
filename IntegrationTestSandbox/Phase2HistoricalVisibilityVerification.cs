using AutoMapper;
using MaterialManagement.BLL.Helper;
using MaterialManagement.BLL.Service.Implementations;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Enums;
using MaterialManagement.DAL.Repo.Implementations;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTestSandbox
{
    internal static class Phase2HistoricalVisibilityVerification
    {
        private const string ConnectionString = "Server=.;Database=MaterialManagementDB;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";

        private static readonly List<TestResult> Results = new();
        private static readonly IMapper Mapper = CreateMapper();

        public static async Task<int> RunAsync()
        {
            var marker = $"P2HV{DateTime.UtcNow:HHmmssfff}";
            Results.Clear();
            Console.WriteLine($"Starting Phase 2 Part C historical visibility verification. Marker={marker}");

            try
            {
                await CleanupAsync(marker);

                await RunSalesInvoiceDetailsWithInactiveMaterialAsync(marker);
                await RunPurchaseInvoiceDetailsWithInactivePartiesAsync(marker);
                await RunReservationHistoryWithInactivePartiesAsync(marker);
                await RunPaymentHistoryWithInactiveAccountHoldersAsync(marker);
                await RunHistoricalReportsWithInactiveRelatedEntitiesAsync(marker);
            }
            finally
            {
                await CleanupAsync(marker);
            }

            Console.WriteLine();
            Console.WriteLine("=== Phase 2 Part C Historical Visibility Verification Results ===");
            foreach (var result in Results)
            {
                Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} | {result.CaseName} | Expected: {result.Expected} | Actual: {result.Actual}");
            }

            var failedCount = Results.Count(result => !result.Passed);
            Console.WriteLine($"SUMMARY: {Results.Count - failedCount}/{Results.Count} checks passed.");
            return failedCount == 0 ? 0 : 1;
        }

        private static async Task RunSalesInvoiceDetailsWithInactiveMaterialAsync(string marker)
        {
            var seed = await SeedSalesInvoiceAsync(marker, "sales-details");
            await DeactivateClientAndMaterialAsync(seed.ClientId, seed.MaterialId);

            await using var context = CreateContext();
            var invoice = await new SalesInvoiceRepo(context).GetByIdWithDetailsAsync(seed.InvoiceId);
            var item = invoice?.SalesInvoiceItems.SingleOrDefault();

            AddResult("1. Sales invoice details - invoice remains visible", "visible", invoice != null ? "visible" : "missing");
            AddResult("1. Sales invoice details - inactive client label preserved", seed.ClientName, invoice?.Client?.Name ?? "missing");
            AddResult("1. Sales invoice details - historical line count", 1, invoice?.SalesInvoiceItems.Count ?? 0);
            AddResult("1. Sales invoice details - inactive material label preserved", seed.MaterialName, item?.Material?.Name ?? "missing");
        }

        private static async Task RunPurchaseInvoiceDetailsWithInactivePartiesAsync(string marker)
        {
            var supplierSeed = await SeedSupplierPurchaseInvoiceAsync(marker, "purchase-supplier-details");
            await DeactivateSupplierAndMaterialAsync(supplierSeed.SupplierId!.Value, supplierSeed.MaterialId);

            var clientSeed = await SeedClientReturnPurchaseInvoiceAsync(marker, "purchase-client-details");
            await DeactivateClientAndMaterialAsync(clientSeed.ClientId!.Value, clientSeed.MaterialId);

            await using var context = CreateContext();
            var repo = new PurchaseInvoiceRepo(context);
            var supplierInvoice = await repo.GetByIdAsync(supplierSeed.InvoiceId);
            var supplierItem = supplierInvoice?.PurchaseInvoiceItems.SingleOrDefault();
            var clientInvoice = await repo.GetByIdAsync(clientSeed.InvoiceId);
            var clientItem = clientInvoice?.PurchaseInvoiceItems.SingleOrDefault();

            AddResult("2. Purchase supplier invoice - invoice remains visible", "visible", supplierInvoice != null ? "visible" : "missing");
            AddResult("2. Purchase supplier invoice - inactive supplier label preserved", supplierSeed.SupplierName, supplierInvoice?.Supplier?.Name ?? "missing");
            AddResult("2. Purchase supplier invoice - inactive material label preserved", supplierSeed.MaterialName, supplierItem?.Material?.Name ?? "missing");
            AddResult("2. Purchase client return - invoice remains visible", "visible", clientInvoice != null ? "visible" : "missing");
            AddResult("2. Purchase client return - inactive client label preserved", clientSeed.ClientName, clientInvoice?.Client?.Name ?? "missing");
            AddResult("2. Purchase client return - inactive material label preserved", clientSeed.MaterialName, clientItem?.Material?.Name ?? "missing");
        }

        private static async Task RunReservationHistoryWithInactivePartiesAsync(string marker)
        {
            var seed = await SeedReservationAsync(marker, "reservation-history");
            await DeactivateClientAndMaterialAsync(seed.ClientId, seed.MaterialId);

            await using var context = CreateContext();
            var repo = new ReservationRepo(context);
            var detail = await repo.GetByIdWithDetailsAsync(seed.ReservationId);
            var detailItem = detail?.ReservationItems.SingleOrDefault();
            var activeList = await repo.GetAllActiveWithDetailsAsync();
            var listed = activeList.SingleOrDefault(reservation => reservation.Id == seed.ReservationId);
            var listedItem = listed?.ReservationItems.SingleOrDefault();

            AddResult("3. Reservation details - reservation remains visible", "visible", detail != null ? "visible" : "missing");
            AddResult("3. Reservation details - inactive client label preserved", seed.ClientName, detail?.Client?.Name ?? "missing");
            AddResult("3. Reservation details - inactive material label preserved", seed.MaterialName, detailItem?.Material?.Name ?? "missing");
            AddResult("3. Reservation active history - row remains listed", "listed", listed != null ? "listed" : "missing");
            AddResult("3. Reservation active history - listed item material preserved", seed.MaterialName, listedItem?.Material?.Name ?? "missing");
        }

        private static async Task RunPaymentHistoryWithInactiveAccountHoldersAsync(string marker)
        {
            var paymentSeed = await SeedPaymentHistoryAsync(marker, "payment-history");
            await DeactivateClientAsync(paymentSeed.ClientId);
            await DeactivateSupplierAsync(paymentSeed.SupplierId);

            await using var context = CreateContext();
            var clientPayments = await new ClientPaymentRepo(context).GetByClientIdAsync(paymentSeed.ClientId);
            var supplierPayments = await new SupplierPaymentRepo(context).GetBySupplierIdAsync(paymentSeed.SupplierId);

            AddResult("4. Client payment history - visible after client inactive", 1, clientPayments.Count());
            AddResult("4. Supplier payment history - visible after supplier inactive", 1, supplierPayments.Count());
        }

        private static async Task RunHistoricalReportsWithInactiveRelatedEntitiesAsync(string marker)
        {
            var seed = await SeedReportScenarioAsync(marker, "reports");
            await DeactivateClientAsync(seed.ClientId);
            await DeactivateSupplierAsync(seed.SupplierId);
            await DeactivateMaterialAsync(seed.MaterialId);

            await using var context = CreateContext();
            var service = new ReportService(context, Mapper);
            var fromDate = DateTime.Today.AddDays(-1);
            var toDate = DateTime.Today.AddDays(1);

            var clientStatement = await service.GetClientAccountStatementAsync(seed.ClientId, fromDate, toDate);
            var salesStatementRow = clientStatement.FirstOrDefault(row => row.Reference == seed.SalesInvoiceNumber);

            var materialMovement = await service.GetMaterialMovementAsync(seed.MaterialId, fromDate, toDate);
            var movementInvoiceNumbers = materialMovement
                .Where(row => !string.IsNullOrEmpty(row.InvoiceNumber))
                .Select(row => row.InvoiceNumber)
                .ToHashSet();

            var profitReport = await service.GetProfitReportAsync(fromDate, toDate);
            var profitRow = profitReport.FirstOrDefault(row => row.InvoiceNumber == seed.SalesInvoiceNumber);

            AddResult("5. Client statement - inactive client still reportable", "visible", salesStatementRow != null ? "visible" : "missing");
            AddResult("5. Client statement - inactive material item retained", 1, salesStatementRow?.Items.Count ?? 0);
            AddResult("5. Material movement - inactive material sale retained", "visible", movementInvoiceNumbers.Contains(seed.SalesInvoiceNumber) ? "visible" : "missing");
            AddResult("5. Material movement - inactive material purchase retained", "visible", movementInvoiceNumbers.Contains(seed.PurchaseInvoiceNumber) ? "visible" : "missing");
            AddResult("5. Profit report - inactive client/material invoice retained", "visible", profitRow != null ? "visible" : "missing");
            AddResult("5. Profit report - inactive client label retained", seed.ClientName, profitRow?.ClientName ?? "missing");
        }

        private static async Task<SalesInvoiceSeed> SeedSalesInvoiceAsync(string marker, string suffix)
        {
            await using var context = CreateContext();
            var client = CreateClient(marker, suffix + "-client", 0m);
            var material = CreateMaterial(marker, suffix + "-material", 20m, 5m);
            var invoice = CreateSalesInvoice(marker, suffix, client, material, 2m, 10m);

            context.SalesInvoices.Add(invoice);
            await context.SaveChangesAsync();

            return new SalesInvoiceSeed(invoice.Id, client.Id, material.Id, client.Name, material.Name, invoice.InvoiceNumber);
        }

        private static async Task<PurchaseInvoiceSeed> SeedSupplierPurchaseInvoiceAsync(string marker, string suffix)
        {
            await using var context = CreateContext();
            var supplier = CreateSupplier(marker, suffix + "-supplier", 0m);
            var material = CreateMaterial(marker, suffix + "-material", 20m, 5m);
            var invoice = CreatePurchaseInvoice(marker, suffix, material, 2m, 10m);
            invoice.Supplier = supplier;

            context.PurchaseInvoices.Add(invoice);
            await context.SaveChangesAsync();

            return new PurchaseInvoiceSeed(invoice.Id, supplier.Id, null, material.Id, supplier.Name, null, material.Name, invoice.InvoiceNumber);
        }

        private static async Task<PurchaseInvoiceSeed> SeedClientReturnPurchaseInvoiceAsync(string marker, string suffix)
        {
            await using var context = CreateContext();
            var client = CreateClient(marker, suffix + "-client", 0m);
            var material = CreateMaterial(marker, suffix + "-material", 20m, 5m);
            var invoice = CreatePurchaseInvoice(marker, suffix, material, 2m, 10m);
            invoice.Client = client;

            context.PurchaseInvoices.Add(invoice);
            await context.SaveChangesAsync();

            return new PurchaseInvoiceSeed(invoice.Id, null, client.Id, material.Id, null, client.Name, material.Name, invoice.InvoiceNumber);
        }

        private static async Task<ReservationSeed> SeedReservationAsync(string marker, string suffix)
        {
            await using var context = CreateContext();
            var client = CreateClient(marker, suffix + "-client", 0m);
            var material = CreateMaterial(marker, suffix + "-material", 20m, 5m);
            var reservation = new Reservation
            {
                Client = client,
                ReservationNumber = $"RES-{marker}-{suffix}",
                ReservationDate = DateTime.Now,
                TotalAmount = 20m,
                Status = ReservationStatus.Active,
                Notes = marker,
                ReservationItems = new List<ReservationItem>
                {
                    new ReservationItem
                    {
                        Material = material,
                        Quantity = 2m,
                        UnitPrice = 10m,
                        TotalPrice = 20m,
                        FulfilledQuantity = 0m
                    }
                }
            };

            context.Reservations.Add(reservation);
            await context.SaveChangesAsync();

            return new ReservationSeed(reservation.Id, client.Id, material.Id, client.Name, material.Name);
        }

        private static async Task<PaymentSeed> SeedPaymentHistoryAsync(string marker, string suffix)
        {
            await using var context = CreateContext();
            var client = CreateClient(marker, suffix + "-client", 50m);
            var supplier = CreateSupplier(marker, suffix + "-supplier", 50m);
            context.ClientPayments.Add(new ClientPayment
            {
                Client = client,
                PaymentDate = DateTime.Now,
                Amount = 12m,
                PaymentMethod = "cash",
                Notes = marker
            });
            context.SupplierPayments.Add(new SupplierPayment
            {
                Supplier = supplier,
                PaymentDate = DateTime.Now,
                Amount = 15m,
                PaymentMethod = "cash",
                Notes = marker
            });

            await context.SaveChangesAsync();
            return new PaymentSeed(client.Id, supplier.Id);
        }

        private static async Task<ReportSeed> SeedReportScenarioAsync(string marker, string suffix)
        {
            await using var context = CreateContext();
            var client = CreateClient(marker, suffix + "-client", 0m);
            var supplier = CreateSupplier(marker, suffix + "-supplier", 0m);
            var material = CreateMaterial(marker, suffix + "-material", 30m, 7m);

            var salesInvoice = CreateSalesInvoice(marker, suffix + "-sale", client, material, 2m, 11m);
            var purchaseInvoice = CreatePurchaseInvoice(marker, suffix + "-purchase", material, 3m, 7m);
            purchaseInvoice.Supplier = supplier;

            context.SalesInvoices.Add(salesInvoice);
            context.PurchaseInvoices.Add(purchaseInvoice);
            await context.SaveChangesAsync();

            return new ReportSeed(
                client.Id,
                supplier.Id,
                material.Id,
                client.Name,
                salesInvoice.InvoiceNumber,
                purchaseInvoice.InvoiceNumber);
        }

        private static SalesInvoice CreateSalesInvoice(
            string marker,
            string suffix,
            Client client,
            Material material,
            decimal quantity,
            decimal unitPrice)
        {
            var total = quantity * unitPrice;
            return new SalesInvoice
            {
                Client = client,
                InvoiceNumber = $"SAL-{marker}-{suffix}",
                InvoiceDate = DateTime.Now,
                TotalAmount = total,
                DiscountAmount = 0m,
                PaidAmount = 0m,
                RemainingAmount = total,
                Notes = marker,
                IsActive = true,
                SalesInvoiceItems = new List<SalesInvoiceItem>
                {
                    new SalesInvoiceItem
                    {
                        Material = material,
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        TotalPrice = total
                    }
                }
            };
        }

        private static PurchaseInvoice CreatePurchaseInvoice(
            string marker,
            string suffix,
            Material material,
            decimal quantity,
            decimal unitPrice)
        {
            var total = quantity * unitPrice;
            return new PurchaseInvoice
            {
                InvoiceNumber = $"PUR-{marker}-{suffix}",
                InvoiceDate = DateTime.Now,
                TotalAmount = total,
                DiscountAmount = 0m,
                PaidAmount = 0m,
                RemainingAmount = total,
                Notes = marker,
                IsActive = true,
                PurchaseInvoiceItems = new List<PurchaseInvoiceItem>
                {
                    new PurchaseInvoiceItem
                    {
                        Material = material,
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        TotalPrice = total
                    }
                }
            };
        }

        private static Client CreateClient(string marker, string suffix, decimal balance)
        {
            return new Client
            {
                Name = $"{marker}-{suffix}",
                Phone = CreatePhone(marker, suffix),
                Balance = balance,
                Address = marker,
                IsActive = true
            };
        }

        private static Supplier CreateSupplier(string marker, string suffix, decimal balance)
        {
            return new Supplier
            {
                Name = $"{marker}-{suffix}",
                Phone = CreatePhone(marker, suffix),
                Balance = balance,
                Address = marker,
                IsActive = true
            };
        }

        private static Material CreateMaterial(string marker, string suffix, decimal quantity, decimal price)
        {
            return new Material
            {
                Name = $"{marker}-{suffix}",
                Code = CreateMaterialCode(marker, suffix),
                Unit = "pcs",
                Quantity = quantity,
                ReservedQuantity = 0m,
                PurchasePrice = price,
                SellingPrice = price,
                Description = marker,
                IsActive = true
            };
        }

        private static async Task DeactivateClientAndMaterialAsync(int clientId, int materialId)
        {
            await DeactivateClientAsync(clientId);
            await DeactivateMaterialAsync(materialId);
        }

        private static async Task DeactivateSupplierAndMaterialAsync(int supplierId, int materialId)
        {
            await DeactivateSupplierAsync(supplierId);
            await DeactivateMaterialAsync(materialId);
        }

        private static async Task DeactivateClientAsync(int clientId)
        {
            await using var context = CreateContext();
            var client = await context.Clients.IgnoreQueryFilters().SingleAsync(c => c.Id == clientId);
            client.IsActive = false;
            await context.SaveChangesAsync();
        }

        private static async Task DeactivateSupplierAsync(int supplierId)
        {
            await using var context = CreateContext();
            var supplier = await context.Suppliers.IgnoreQueryFilters().SingleAsync(s => s.Id == supplierId);
            supplier.IsActive = false;
            await context.SaveChangesAsync();
        }

        private static async Task DeactivateMaterialAsync(int materialId)
        {
            await using var context = CreateContext();
            var material = await context.Materials.IgnoreQueryFilters().SingleAsync(m => m.Id == materialId);
            material.IsActive = false;
            await context.SaveChangesAsync();
        }

        private static MaterialManagementContext CreateContext()
        {
            var builder = new DbContextOptionsBuilder<MaterialManagementContext>()
                .UseSqlServer(ConnectionString)
                .EnableSensitiveDataLogging();

            return new MaterialManagementContext(builder.Options);
        }

        private static async Task CleanupAsync(string marker)
        {
            await using var context = CreateContext();

            var clientIds = await context.Clients
                .IgnoreQueryFilters()
                .Where(client => client.Address == marker || client.Name.Contains(marker))
                .Select(client => client.Id)
                .ToListAsync();

            var supplierIds = await context.Suppliers
                .IgnoreQueryFilters()
                .Where(supplier => supplier.Address == marker || supplier.Name.Contains(marker))
                .Select(supplier => supplier.Id)
                .ToListAsync();

            var materialIds = await context.Materials
                .IgnoreQueryFilters()
                .Where(material => material.Description == marker || material.Code.Contains(marker))
                .Select(material => material.Id)
                .ToListAsync();

            var salesInvoiceIds = await context.SalesInvoices
                .IgnoreQueryFilters()
                .Where(invoice => invoice.Notes == marker || invoice.InvoiceNumber.Contains(marker) || (invoice.ClientId.HasValue && clientIds.Contains(invoice.ClientId.Value)))
                .Select(invoice => invoice.Id)
                .ToListAsync();

            var purchaseInvoiceIds = await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(invoice =>
                    invoice.Notes == marker ||
                    invoice.InvoiceNumber.Contains(marker) ||
                    (invoice.SupplierId.HasValue && supplierIds.Contains(invoice.SupplierId.Value)) ||
                    (invoice.ClientId.HasValue && clientIds.Contains(invoice.ClientId.Value)))
                .Select(invoice => invoice.Id)
                .ToListAsync();

            var reservationIds = await context.Reservations
                .IgnoreQueryFilters()
                .Where(reservation => reservation.Notes == marker || clientIds.Contains(reservation.ClientId))
                .Select(reservation => reservation.Id)
                .ToListAsync();

            var clientPayments = await context.ClientPayments
                .IgnoreQueryFilters()
                .Where(payment =>
                    clientIds.Contains(payment.ClientId) ||
                    (payment.SalesInvoiceId.HasValue && salesInvoiceIds.Contains(payment.SalesInvoiceId.Value)) ||
                    payment.Notes == marker)
                .ToListAsync();
            context.ClientPayments.RemoveRange(clientPayments);

            var supplierPayments = await context.SupplierPayments
                .IgnoreQueryFilters()
                .Where(payment =>
                    supplierIds.Contains(payment.SupplierId) ||
                    (payment.PurchaseInvoiceId.HasValue && purchaseInvoiceIds.Contains(payment.PurchaseInvoiceId.Value)) ||
                    payment.Notes == marker)
                .ToListAsync();
            context.SupplierPayments.RemoveRange(supplierPayments);

            var reservationItems = await context.ReservationItems
                .IgnoreQueryFilters()
                .Where(item => reservationIds.Contains(item.ReservationId))
                .ToListAsync();
            context.ReservationItems.RemoveRange(reservationItems);

            var reservations = await context.Reservations
                .IgnoreQueryFilters()
                .Where(reservation => reservationIds.Contains(reservation.Id))
                .ToListAsync();
            context.Reservations.RemoveRange(reservations);

            var salesInvoiceItems = await context.SalesInvoiceItems
                .IgnoreQueryFilters()
                .Where(item => salesInvoiceIds.Contains(item.SalesInvoiceId))
                .ToListAsync();
            context.SalesInvoiceItems.RemoveRange(salesInvoiceItems);

            var purchaseInvoiceItems = await context.PurchaseInvoiceItems
                .IgnoreQueryFilters()
                .Where(item => purchaseInvoiceIds.Contains(item.PurchaseInvoiceId))
                .ToListAsync();
            context.PurchaseInvoiceItems.RemoveRange(purchaseInvoiceItems);

            var salesInvoices = await context.SalesInvoices
                .IgnoreQueryFilters()
                .Where(invoice => salesInvoiceIds.Contains(invoice.Id))
                .ToListAsync();
            context.SalesInvoices.RemoveRange(salesInvoices);

            var purchaseInvoices = await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(invoice => purchaseInvoiceIds.Contains(invoice.Id))
                .ToListAsync();
            context.PurchaseInvoices.RemoveRange(purchaseInvoices);

            var clients = await context.Clients
                .IgnoreQueryFilters()
                .Where(client => clientIds.Contains(client.Id))
                .ToListAsync();
            context.Clients.RemoveRange(clients);

            var suppliers = await context.Suppliers
                .IgnoreQueryFilters()
                .Where(supplier => supplierIds.Contains(supplier.Id))
                .ToListAsync();
            context.Suppliers.RemoveRange(suppliers);

            var materials = await context.Materials
                .IgnoreQueryFilters()
                .Where(material => materialIds.Contains(material.Id))
                .ToListAsync();
            context.Materials.RemoveRange(materials);

            await context.SaveChangesAsync();
        }

        private static void AddResult<T>(string caseName, T expected, T actual)
        {
            Results.Add(new TestResult(
                caseName,
                expected?.ToString() ?? string.Empty,
                actual?.ToString() ?? string.Empty,
                EqualityComparer<T>.Default.Equals(expected, actual)));
        }

        private static IMapper CreateMapper()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfile>());
            return config.CreateMapper();
        }

        private static string CreatePhone(string marker, string suffix)
        {
            var hash = ((uint)HashCode.Combine(marker, suffix)).ToString();
            return $"8{hash}".PadRight(15, '0').Substring(0, 15);
        }

        private static string CreateMaterialCode(string marker, string suffix)
        {
            var maxSuffixLength = Math.Max(1, 49 - marker.Length);
            var normalizedSuffix = suffix.Length <= maxSuffixLength ? suffix : suffix.Substring(0, maxSuffixLength);
            return $"{marker}-{normalizedSuffix}";
        }

        private sealed record TestResult(string CaseName, string Expected, string Actual, bool Passed);

        private sealed record SalesInvoiceSeed(
            int InvoiceId,
            int ClientId,
            int MaterialId,
            string ClientName,
            string MaterialName,
            string SalesInvoiceNumber);

        private sealed record PurchaseInvoiceSeed(
            int InvoiceId,
            int? SupplierId,
            int? ClientId,
            int MaterialId,
            string? SupplierName,
            string? ClientName,
            string MaterialName,
            string PurchaseInvoiceNumber);

        private sealed record ReservationSeed(
            int ReservationId,
            int ClientId,
            int MaterialId,
            string ClientName,
            string MaterialName);

        private sealed record PaymentSeed(int ClientId, int SupplierId);

        private sealed record ReportSeed(
            int ClientId,
            int SupplierId,
            int MaterialId,
            string ClientName,
            string SalesInvoiceNumber,
            string PurchaseInvoiceNumber);
    }
}
