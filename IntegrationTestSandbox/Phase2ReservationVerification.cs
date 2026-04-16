using AutoMapper;
using MaterialManagement.BLL.Helper;
using MaterialManagement.BLL.ModelVM.Reservation;
using MaterialManagement.BLL.Service.Implementations;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Enums;
using MaterialManagement.DAL.Repo.Implementations;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTestSandbox
{
    internal static class Phase2ReservationVerification
    {
        private const string ConnectionString = "Server=.;Database=MaterialManagementDB;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";

        private static readonly List<TestResult> Results = new();
        private static readonly IMapper Mapper = CreateMapper();

        public static async Task<int> RunAsync()
        {
            var marker = $"P2R{DateTime.UtcNow:HHmmssfff}";
            Results.Clear();
            Console.WriteLine($"Starting Phase 2 Part B reservation verification. Marker={marker}");

            try
            {
                await CleanupAsync(marker);

                await RunValidReservationCreateAsync(marker);
                await RunAggregateOverReservationCreateAsync(marker);
                await RunReservationUpdateAsync(marker);
                await RunPartialUpdateBlockedAsync(marker);
                await RunPartialFulfillmentSuccessAsync(marker);
                await RunPartialFulfillmentNegativeStockFailureAsync(marker);
                await RunCancelRemainingOnlyAsync(marker);
                await RunFullFulfillmentSuccessAsync(marker);
                await RunFullFulfillmentNegativeStockFailureAsync(marker);
            }
            finally
            {
                await CleanupAsync(marker);
            }

            Console.WriteLine();
            Console.WriteLine("=== Phase 2 Part B Reservation Verification Results ===");
            foreach (var result in Results)
            {
                Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} | {result.CaseName} | Expected: {result.Expected} | Actual: {result.Actual}");
            }

            var failedCount = Results.Count(result => !result.Passed);
            Console.WriteLine($"SUMMARY: {Results.Count - failedCount}/{Results.Count} checks passed.");
            return failedCount == 0 ? 0 : 1;
        }

        private static async Task RunValidReservationCreateAsync(string marker)
        {
            var clientId = await SeedClientAsync(marker, "valid-create-client", 100m);
            var materialId = await SeedMaterialAsync(marker, "valid-create-material", 10m, 0m, 20m);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).CreateReservationAsync(new ReservationCreateModel
                {
                    ClientId = clientId,
                    Notes = marker,
                    Items = new List<ReservationItemModel>
                    {
                        new ReservationItemModel { MaterialId = materialId, Quantity = 4m, UnitPrice = 20m }
                    }
                });
            });

            var reservation = await GetLatestReservationForClientAsync(clientId);
            var material = await GetMaterialSnapshotAsync(materialId);
            AddResult("1. Valid reservation create - succeeds", "success", result.Success ? "success" : result.Message);
            AddResult("1. Valid reservation create - reserved quantity", 4m, material.ReservedQuantity);
            AddResult("1. Valid reservation create - physical stock unchanged", 10m, material.Quantity);
            AddResult("1. Valid reservation create - client balance unchanged", 100m, await GetClientBalanceAsync(clientId));
            AddResult("1. Valid reservation create - total amount", 80m, reservation.TotalAmount);
        }

        private static async Task RunAggregateOverReservationCreateAsync(string marker)
        {
            var clientId = await SeedClientAsync(marker, "over-create-client", 100m);
            var materialId = await SeedMaterialAsync(marker, "over-create-material", 5m, 0m, 20m);
            var countBefore = await CountReservationsForClientAsync(clientId);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).CreateReservationAsync(new ReservationCreateModel
                {
                    ClientId = clientId,
                    Notes = marker,
                    Items = new List<ReservationItemModel>
                    {
                        new ReservationItemModel { MaterialId = materialId, Quantity = 3m, UnitPrice = 20m },
                        new ReservationItemModel { MaterialId = materialId, Quantity = 3m, UnitPrice = 20m }
                    }
                });
            });

            var material = await GetMaterialSnapshotAsync(materialId);
            AddResult("2. Aggregate over-reservation create - fails", "failure", result.Success ? "success" : "failure");
            AddResult("2. Aggregate over-reservation create - no reservation persisted", countBefore, await CountReservationsForClientAsync(clientId));
            AddResult("2. Aggregate over-reservation create - reserved unchanged", 0m, material.ReservedQuantity);
            AddResult("2. Aggregate over-reservation create - balance unchanged", 100m, await GetClientBalanceAsync(clientId));
        }

        private static async Task RunReservationUpdateAsync(string marker)
        {
            var seed = await CreateReservationSeedAsync(marker, "update", 100m, 10m, 0m, 3m, 20m);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).UpdateReservationAsync(new ReservationUpdateModel
                {
                    Id = seed.ReservationId,
                    ClientId = seed.ClientId,
                    Notes = marker,
                    Items = new List<ReservationItemModel>
                    {
                        new ReservationItemModel { MaterialId = seed.MaterialId, Quantity = 5m, UnitPrice = 22m }
                    }
                });
            });

            var reservation = await GetReservationAsync(seed.ReservationId);
            var material = await GetMaterialSnapshotAsync(seed.MaterialId);
            AddResult("3. Reservation update - succeeds", "success", result.Success ? "success" : result.Message);
            AddResult("3. Reservation update - reserved quantity replaced", 5m, material.ReservedQuantity);
            AddResult("3. Reservation update - total amount recalculated", 110m, reservation.TotalAmount);
            AddResult("3. Reservation update - client balance unchanged", 100m, await GetClientBalanceAsync(seed.ClientId));
        }

        private static async Task RunPartialUpdateBlockedAsync(string marker)
        {
            var seed = await CreateReservationSeedAsync(marker, "partial-update-block", 100m, 10m, 0m, 5m, 10m);
            await PartialFulfillAsync(seed.ReservationId, seed.ReservationItemId, 2m);
            var materialBefore = await GetMaterialSnapshotAsync(seed.MaterialId);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).UpdateReservationAsync(new ReservationUpdateModel
                {
                    Id = seed.ReservationId,
                    ClientId = seed.ClientId,
                    Notes = marker,
                    Items = new List<ReservationItemModel>
                    {
                        new ReservationItemModel { MaterialId = seed.MaterialId, Quantity = 4m, UnitPrice = 10m }
                    }
                });
            });

            var materialAfter = await GetMaterialSnapshotAsync(seed.MaterialId);
            AddResult("4. Partially fulfilled reservation update - fails", "failure", result.Success ? "success" : "failure");
            AddResult("4. Partially fulfilled reservation update - reserved unchanged", materialBefore.ReservedQuantity, materialAfter.ReservedQuantity);
            AddResult("4. Partially fulfilled reservation update - stock unchanged", materialBefore.Quantity, materialAfter.Quantity);
        }

        private static async Task RunPartialFulfillmentSuccessAsync(string marker)
        {
            var seed = await CreateReservationSeedAsync(marker, "partial-success", 100m, 10m, 0m, 5m, 10m);
            var invoiceCountBefore = await CountSalesInvoicesForClientAsync(seed.ClientId);

            var result = await TryRunAsync(async () => await PartialFulfillAsync(seed.ReservationId, seed.ReservationItemId, 3m));

            var material = await GetMaterialSnapshotAsync(seed.MaterialId);
            var item = await GetReservationItemAsync(seed.ReservationItemId);
            var invoice = await GetLatestSalesInvoiceForClientAsync(seed.ClientId);
            AddResult("5. Partial fulfillment success - succeeds", "success", result.Success ? "success" : result.Message);
            AddResult("5. Partial fulfillment success - stock reduced", 7m, material.Quantity);
            AddResult("5. Partial fulfillment success - reserved reduced", 2m, material.ReservedQuantity);
            AddResult("5. Partial fulfillment success - fulfilled quantity", 3m, item.FulfilledQuantity ?? 0m);
            AddResult("5. Partial fulfillment success - invoice created", invoiceCountBefore + 1, await CountSalesInvoicesForClientAsync(seed.ClientId));
            AddResult("5. Partial fulfillment success - invoice total", 30m, invoice.TotalAmount);
            AddResult("5. Partial fulfillment success - invoice paid", 0m, invoice.PaidAmount);
            AddResult("5. Partial fulfillment success - invoice remaining", 30m, invoice.RemainingAmount);
            AddResult("5. Partial fulfillment success - client balance increased by invoice", 130m, await GetClientBalanceAsync(seed.ClientId));
            AddResult("5. Partial fulfillment success - reservation remains active", ReservationStatus.Active, (await GetReservationAsync(seed.ReservationId)).Status);
        }

        private static async Task RunPartialFulfillmentNegativeStockFailureAsync(string marker)
        {
            var seed = await CreateReservationSeedAsync(marker, "partial-negative-stock", 100m, 5m, 0m, 5m, 10m);
            await SetMaterialStockAsync(seed.MaterialId, 2m, 5m);
            var invoiceCountBefore = await CountSalesInvoicesForClientAsync(seed.ClientId);

            var result = await TryRunAsync(async () => await PartialFulfillAsync(seed.ReservationId, seed.ReservationItemId, 3m));

            var material = await GetMaterialSnapshotAsync(seed.MaterialId);
            var item = await GetReservationItemAsync(seed.ReservationItemId);
            AddResult("6. Partial fulfillment negative stock - fails", "failure", result.Success ? "success" : "failure");
            AddResult("6. Partial fulfillment negative stock - stock unchanged", 2m, material.Quantity);
            AddResult("6. Partial fulfillment negative stock - reserved unchanged", 5m, material.ReservedQuantity);
            AddResult("6. Partial fulfillment negative stock - fulfilled unchanged", 0m, item.FulfilledQuantity ?? 0m);
            AddResult("6. Partial fulfillment negative stock - no invoice", invoiceCountBefore, await CountSalesInvoicesForClientAsync(seed.ClientId));
            AddResult("6. Partial fulfillment negative stock - balance unchanged", 100m, await GetClientBalanceAsync(seed.ClientId));
        }

        private static async Task RunCancelRemainingOnlyAsync(string marker)
        {
            var seed = await CreateReservationSeedAsync(marker, "cancel-remaining", 100m, 10m, 0m, 5m, 10m);
            await PartialFulfillAsync(seed.ReservationId, seed.ReservationItemId, 3m);
            var balanceBeforeCancel = await GetClientBalanceAsync(seed.ClientId);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).CancelReservationAsync(seed.ReservationId);
            });

            var material = await GetMaterialSnapshotAsync(seed.MaterialId);
            var reservation = await GetReservationAsync(seed.ReservationId);
            AddResult("7. Cancel remaining only - succeeds", "success", result.Success ? "success" : result.Message);
            AddResult("7. Cancel remaining only - releases remaining reserved", 0m, material.ReservedQuantity);
            AddResult("7. Cancel remaining only - keeps delivered stock consumed", 7m, material.Quantity);
            AddResult("7. Cancel remaining only - status cancelled", ReservationStatus.Cancelled, reservation.Status);
            AddResult("7. Cancel remaining only - balance unchanged by cancel", balanceBeforeCancel, await GetClientBalanceAsync(seed.ClientId));
        }

        private static async Task RunFullFulfillmentSuccessAsync(string marker)
        {
            var seed = await CreateReservationSeedAsync(marker, "full-success", 100m, 8m, 0m, 4m, 15m);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).FulfillReservationAsync(seed.ReservationId);
            });

            var material = await GetMaterialSnapshotAsync(seed.MaterialId);
            var invoice = await GetLatestSalesInvoiceForClientAsync(seed.ClientId);
            AddResult("8. Full fulfillment success - succeeds", "success", result.Success ? "success" : result.Message);
            AddResult("8. Full fulfillment success - stock reduced", 4m, material.Quantity);
            AddResult("8. Full fulfillment success - reserved zero", 0m, material.ReservedQuantity);
            AddResult("8. Full fulfillment success - status fulfilled", ReservationStatus.Fulfilled, (await GetReservationAsync(seed.ReservationId)).Status);
            AddResult("8. Full fulfillment success - invoice unpaid", "0/60", $"{invoice.PaidAmount:0}/{invoice.RemainingAmount:0}");
            AddResult("8. Full fulfillment success - client balance increased by invoice", 160m, await GetClientBalanceAsync(seed.ClientId));
        }

        private static async Task RunFullFulfillmentNegativeStockFailureAsync(string marker)
        {
            var seed = await CreateReservationSeedAsync(marker, "full-negative-stock", 100m, 5m, 0m, 5m, 10m);
            await SetMaterialStockAsync(seed.MaterialId, 4m, 5m);
            var invoiceCountBefore = await CountSalesInvoicesForClientAsync(seed.ClientId);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).FulfillReservationAsync(seed.ReservationId);
            });

            var material = await GetMaterialSnapshotAsync(seed.MaterialId);
            AddResult("9. Full fulfillment negative stock - fails", "failure", result.Success ? "success" : "failure");
            AddResult("9. Full fulfillment negative stock - stock unchanged", 4m, material.Quantity);
            AddResult("9. Full fulfillment negative stock - reserved unchanged", 5m, material.ReservedQuantity);
            AddResult("9. Full fulfillment negative stock - no invoice", invoiceCountBefore, await CountSalesInvoicesForClientAsync(seed.ClientId));
            AddResult("9. Full fulfillment negative stock - status active", ReservationStatus.Active, (await GetReservationAsync(seed.ReservationId)).Status);
            AddResult("9. Full fulfillment negative stock - balance unchanged", 100m, await GetClientBalanceAsync(seed.ClientId));
        }

        private static async Task PartialFulfillAsync(int reservationId, int reservationItemId, decimal quantity)
        {
            await using var context = CreateContext();
            await CreateService(context).PartialFulfillReservationAsync(
                reservationId,
                new List<ReservationFulfillmentModel>
                {
                    new ReservationFulfillmentModel
                    {
                        ReservationItemId = reservationItemId,
                        QuantityToFulfill = quantity
                    }
                });
        }

        private static async Task<ReservationSeed> CreateReservationSeedAsync(
            string marker,
            string suffix,
            decimal clientBalance,
            decimal materialQuantity,
            decimal materialReservedQuantity,
            decimal reservationQuantity,
            decimal unitPrice)
        {
            var clientId = await SeedClientAsync(marker, suffix + "-client", clientBalance);
            var materialId = await SeedMaterialAsync(marker, suffix + "-material", materialQuantity, materialReservedQuantity, unitPrice);

            await using var context = CreateContext();
            await CreateService(context).CreateReservationAsync(new ReservationCreateModel
            {
                ClientId = clientId,
                Notes = marker,
                Items = new List<ReservationItemModel>
                {
                    new ReservationItemModel
                    {
                        MaterialId = materialId,
                        Quantity = reservationQuantity,
                        UnitPrice = unitPrice
                    }
                }
            });

            var reservation = await GetLatestReservationForClientAsync(clientId);
            var reservationItemId = await GetReservationItemIdAsync(reservation.Id);
            return new ReservationSeed(reservation.Id, reservationItemId, clientId, materialId);
        }

        private static ReservationService CreateService(MaterialManagementContext context)
        {
            return new ReservationService(
                new ReservationRepo(context),
                new MaterialRepo(context),
                new ClientRepo(context),
                context,
                Mapper);
        }

        private static async Task<int> SeedClientAsync(string marker, string suffix, decimal balance)
        {
            await using var context = CreateContext();
            var client = new Client
            {
                Name = $"{marker}-{suffix}",
                Phone = CreatePhone(marker, suffix),
                Balance = balance,
                Address = marker,
                IsActive = true
            };

            context.Clients.Add(client);
            await context.SaveChangesAsync();
            return client.Id;
        }

        private static async Task<int> SeedMaterialAsync(
            string marker,
            string suffix,
            decimal quantity,
            decimal reservedQuantity,
            decimal price)
        {
            await using var context = CreateContext();
            var material = new Material
            {
                Name = $"{marker}-{suffix}",
                Code = CreateMaterialCode(marker, suffix),
                Unit = "pcs",
                Quantity = quantity,
                ReservedQuantity = reservedQuantity,
                PurchasePrice = price,
                SellingPrice = price,
                Description = marker,
                IsActive = true
            };

            context.Materials.Add(material);
            await context.SaveChangesAsync();
            return material.Id;
        }

        private static async Task<Reservation> GetLatestReservationForClientAsync(int clientId)
        {
            await using var context = CreateContext();
            return await context.Reservations
                .IgnoreQueryFilters()
                .Where(reservation => reservation.ClientId == clientId)
                .OrderByDescending(reservation => reservation.Id)
                .SingleAsync();
        }

        private static async Task<Reservation> GetReservationAsync(int reservationId)
        {
            await using var context = CreateContext();
            return await context.Reservations
                .IgnoreQueryFilters()
                .SingleAsync(reservation => reservation.Id == reservationId);
        }

        private static async Task<ReservationItem> GetReservationItemAsync(int reservationItemId)
        {
            await using var context = CreateContext();
            return await context.ReservationItems
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == reservationItemId);
        }

        private static async Task<int> GetReservationItemIdAsync(int reservationId)
        {
            await using var context = CreateContext();
            return await context.ReservationItems
                .IgnoreQueryFilters()
                .Where(item => item.ReservationId == reservationId)
                .Select(item => item.Id)
                .SingleAsync();
        }

        private static async Task<int> CountReservationsForClientAsync(int clientId)
        {
            await using var context = CreateContext();
            return await context.Reservations
                .IgnoreQueryFilters()
                .CountAsync(reservation => reservation.ClientId == clientId);
        }

        private static async Task<SalesInvoice> GetLatestSalesInvoiceForClientAsync(int clientId)
        {
            await using var context = CreateContext();
            return await context.SalesInvoices
                .IgnoreQueryFilters()
                .Where(invoice => invoice.ClientId == clientId)
                .OrderByDescending(invoice => invoice.Id)
                .SingleAsync();
        }

        private static async Task<int> CountSalesInvoicesForClientAsync(int clientId)
        {
            await using var context = CreateContext();
            return await context.SalesInvoices
                .IgnoreQueryFilters()
                .CountAsync(invoice => invoice.ClientId == clientId);
        }

        private static async Task<decimal> GetClientBalanceAsync(int clientId)
        {
            await using var context = CreateContext();
            return await context.Clients
                .IgnoreQueryFilters()
                .Where(client => client.Id == clientId)
                .Select(client => client.Balance)
                .SingleAsync();
        }

        private static async Task<MaterialSnapshot> GetMaterialSnapshotAsync(int materialId)
        {
            await using var context = CreateContext();
            return await context.Materials
                .IgnoreQueryFilters()
                .Where(material => material.Id == materialId)
                .Select(material => new MaterialSnapshot(material.Quantity, material.ReservedQuantity))
                .SingleAsync();
        }

        private static async Task SetMaterialStockAsync(int materialId, decimal quantity, decimal reservedQuantity)
        {
            await using var context = CreateContext();
            var material = await context.Materials
                .IgnoreQueryFilters()
                .SingleAsync(material => material.Id == materialId);
            material.Quantity = quantity;
            material.ReservedQuantity = reservedQuantity;
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

            var materialIds = await context.Materials
                .IgnoreQueryFilters()
                .Where(material => material.Description == marker || material.Code.Contains(marker))
                .Select(material => material.Id)
                .ToListAsync();

            var reservationIds = await context.Reservations
                .IgnoreQueryFilters()
                .Where(reservation => clientIds.Contains(reservation.ClientId) || reservation.Notes == marker)
                .Select(reservation => reservation.Id)
                .ToListAsync();

            var salesInvoiceIds = await context.SalesInvoices
                .IgnoreQueryFilters()
                .Where(invoice => invoice.ClientId.HasValue && clientIds.Contains(invoice.ClientId.Value))
                .Select(invoice => invoice.Id)
                .ToListAsync();

            var salesInvoiceItems = await context.SalesInvoiceItems
                .IgnoreQueryFilters()
                .Where(item => salesInvoiceIds.Contains(item.SalesInvoiceId))
                .ToListAsync();
            context.SalesInvoiceItems.RemoveRange(salesInvoiceItems);

            var salesInvoices = await context.SalesInvoices
                .IgnoreQueryFilters()
                .Where(invoice => salesInvoiceIds.Contains(invoice.Id))
                .ToListAsync();
            context.SalesInvoices.RemoveRange(salesInvoices);

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

            var clients = await context.Clients
                .IgnoreQueryFilters()
                .Where(client => clientIds.Contains(client.Id))
                .ToListAsync();
            context.Clients.RemoveRange(clients);

            var materials = await context.Materials
                .IgnoreQueryFilters()
                .Where(material => materialIds.Contains(material.Id))
                .ToListAsync();
            context.Materials.RemoveRange(materials);

            await context.SaveChangesAsync();
        }

        private static async Task<CommandAttempt> TryRunAsync(Func<Task> action)
        {
            try
            {
                await action();
                return new CommandAttempt(true, "success");
            }
            catch (Exception ex)
            {
                return new CommandAttempt(false, $"{ex.GetType().Name}: {ex.Message}");
            }
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
            var hash = Math.Abs(HashCode.Combine(marker, suffix)).ToString();
            return $"6{hash}".PadRight(15, '0').Substring(0, 15);
        }

        private static string CreateMaterialCode(string marker, string suffix)
        {
            var normalizedSuffix = suffix.Length <= 28 ? suffix : suffix.Substring(0, 28);
            return $"{marker}-{normalizedSuffix}";
        }

        private sealed record TestResult(string CaseName, string Expected, string Actual, bool Passed);

        private sealed record CommandAttempt(bool Success, string Message);

        private sealed record ReservationSeed(int ReservationId, int ReservationItemId, int ClientId, int MaterialId);

        private sealed record MaterialSnapshot(decimal Quantity, decimal ReservedQuantity);
    }
}
