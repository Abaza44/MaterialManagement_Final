using MaterialManagement.BLL.Features.Returns.Commands;
using MaterialManagement.BLL.ModelVM.Returns;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationTestSandbox
{
    internal class Program
    {
        private const string ConnectionString = "Server=.;Database=MaterialManagementDB;Trusted_Connection=True;TrustServerCertificate=True;";

        private static readonly List<TestResult> Results = new();

        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (args.Any(arg => string.Equals(arg, "phase1", StringComparison.OrdinalIgnoreCase)))
            {
                return await Phase1FinancialGuardVerification.RunAsync();
            }

            if (args.Any(arg => string.Equals(arg, "phase2-purchase", StringComparison.OrdinalIgnoreCase)))
            {
                return await Phase2PurchaseInvoiceVerification.RunAsync();
            }

            if (args.Any(arg => string.Equals(arg, "phase2-reservations", StringComparison.OrdinalIgnoreCase)))
            {
                return await Phase2ReservationVerification.RunAsync();
            }

            if (args.Any(arg => string.Equals(arg, "phase2-historical-visibility", StringComparison.OrdinalIgnoreCase)))
            {
                return await Phase2HistoricalVisibilityVerification.RunAsync();
            }

            if (args.Any(arg => string.Equals(arg, "phase2-report-math", StringComparison.OrdinalIgnoreCase)))
            {
                return await Phase2ReportMathVerification.RunAsync();
            }

            if (args.Any(arg => string.Equals(arg, "one-time-party", StringComparison.OrdinalIgnoreCase)))
            {
                return await OneTimePartySmokeVerification.RunAsync();
            }

            var runMarker = $"SRT{DateTime.UtcNow:HHmmssfff}";
            Console.WriteLine($"Starting SalesReturn sandbox runtime verification. Marker={runMarker}");

            try
            {
                await CleanupAsync(runMarker);

                await RunValidPartialReturnAsync(runMarker);
                await RunDiscountedInvoiceReturnAsync(runMarker);
                await RunMultipleReturnsAsync(runMarker);
                await RunWrongInvoiceItemAsync(runMarker);
                await RunSoftDeletedInvoiceAsync(runMarker);
                await RunInactiveMaterialAsync(runMarker);
                await RunRollbackProofAsync(runMarker);
                await RunConcurrencyScenarioAsync(runMarker);
            }
            finally
            {
                await CleanupAsync(runMarker);
            }

            Console.WriteLine();
            Console.WriteLine("=== SalesReturn Sandbox Runtime Verification Results ===");
            foreach (var result in Results)
            {
                Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} | {result.CaseName} | Expected: {result.Expected} | Actual: {result.Actual}");
            }

            var failedCount = Results.Count(r => !r.Passed);
            Console.WriteLine($"SUMMARY: {Results.Count - failedCount}/{Results.Count} checks passed.");
            return failedCount == 0 ? 0 : 1;
        }

        private static async Task RunValidPartialReturnAsync(string marker)
        {
            var seed = await SeedInvoiceAsync(marker, "valid-partial", 1000m, 0m, 0m, new[]
            {
                new InvoiceItemSeed("VP-MAT-1", 100m, 10m, 50m)
            });

            var stockBeforeReturn = await GetMaterialQuantityAsync(seed.Items[0].MaterialId);
            var balanceBeforeReturn = await GetClientBalanceAsync(seed.ClientId);

            var result = await SendCommandAsync(new SalesReturnCreateModel
            {
                SalesInvoiceId = seed.InvoiceId,
                Notes = $"{marker}: valid partial return",
                Items = new List<SalesReturnItemCreateModel>
                {
                    new SalesReturnItemCreateModel { SalesInvoiceItemId = seed.Items[0].InvoiceItemId, ReturnedQuantity = 3m }
                }
            });

            var stockAfterReturn = await GetMaterialQuantityAsync(seed.Items[0].MaterialId);
            var balanceAfterReturn = await GetClientBalanceAsync(seed.ClientId);
            var persisted = await GetReturnSnapshotAsync(result.Id);

            AddResult("1. Valid partial return - header", "SalesReturn persisted", persisted.ReturnExists ? "SalesReturn persisted" : "SalesReturn missing");
            AddResult("1. Valid partial return - items", "1 SalesReturnItem", $"{persisted.ItemCount} SalesReturnItem");
            AddResult("1. Valid partial return - stock", stockBeforeReturn + 3m, stockAfterReturn);
            AddResult("1. Valid partial return - client balance", balanceBeforeReturn - 150m, balanceAfterReturn);
            AddResult("1. Valid partial return - net total", 150m, persisted.TotalNetAmount);
        }

        private static async Task RunDiscountedInvoiceReturnAsync(string marker)
        {
            var seed = await SeedInvoiceAsync(marker, "discounted", 500m, 0m, 10m, new[]
            {
                new InvoiceItemSeed("DISC-MAT-1", 50m, 4m, 25m)
            });

            var balanceBeforeReturn = await GetClientBalanceAsync(seed.ClientId);

            var result = await SendCommandAsync(new SalesReturnCreateModel
            {
                SalesInvoiceId = seed.InvoiceId,
                Notes = $"{marker}: discounted return",
                Items = new List<SalesReturnItemCreateModel>
                {
                    new SalesReturnItemCreateModel { SalesInvoiceItemId = seed.Items[0].InvoiceItemId, ReturnedQuantity = 2m }
                }
            });

            var persisted = await GetReturnSnapshotAsync(result.Id);
            var line = persisted.Items.Single();
            var balanceAfterReturn = await GetClientBalanceAsync(seed.ClientId);

            AddResult("2. Discounted return - net unit price", 22.50m, line.NetUnitPrice);
            AddResult("2. Discounted return - total net amount", 45.00m, persisted.TotalNetAmount);
            AddResult("2. Discounted return - prorated discount", 5.00m, persisted.TotalProratedDiscount);
            AddResult("2. Discounted return - client balance", balanceBeforeReturn - 45.00m, balanceAfterReturn);
        }

        private static async Task RunMultipleReturnsAsync(string marker)
        {
            var seed = await SeedInvoiceAsync(marker, "multiple", 200m, 0m, 0m, new[]
            {
                new InvoiceItemSeed("MULTI-MAT-1", 40m, 10m, 20m)
            });

            var first = await TrySendCommandAsync(new SalesReturnCreateModel
            {
                SalesInvoiceId = seed.InvoiceId,
                Notes = $"{marker}: first return",
                Items = new List<SalesReturnItemCreateModel>
                {
                    new SalesReturnItemCreateModel { SalesInvoiceItemId = seed.Items[0].InvoiceItemId, ReturnedQuantity = 4m }
                }
            });

            var second = await TrySendCommandAsync(new SalesReturnCreateModel
            {
                SalesInvoiceId = seed.InvoiceId,
                Notes = $"{marker}: second return",
                Items = new List<SalesReturnItemCreateModel>
                {
                    new SalesReturnItemCreateModel { SalesInvoiceItemId = seed.Items[0].InvoiceItemId, ReturnedQuantity = 3m }
                }
            });

            var returnCountBeforeOverReturn = await CountReturnsForInvoiceAsync(seed.InvoiceId);
            var stockBeforeOverReturn = await GetMaterialQuantityAsync(seed.Items[0].MaterialId);
            var balanceBeforeOverReturn = await GetClientBalanceAsync(seed.ClientId);

            var overReturn = await TrySendCommandAsync(new SalesReturnCreateModel
            {
                SalesInvoiceId = seed.InvoiceId,
                Notes = $"{marker}: over return",
                Items = new List<SalesReturnItemCreateModel>
                {
                    new SalesReturnItemCreateModel { SalesInvoiceItemId = seed.Items[0].InvoiceItemId, ReturnedQuantity = 4m }
                }
            });

            var returnCountAfterOverReturn = await CountReturnsForInvoiceAsync(seed.InvoiceId);
            var stockAfterOverReturn = await GetMaterialQuantityAsync(seed.Items[0].MaterialId);
            var balanceAfterOverReturn = await GetClientBalanceAsync(seed.ClientId);

            AddResult("3. Multiple returns - first succeeds", "success", first.Success ? "success" : first.Error);
            AddResult("3. Multiple returns - second succeeds", "success", second.Success ? "success" : second.Error);
            AddResult("3. Multiple returns - over-return fails", "failure", overReturn.Success ? "success" : "failure");
            AddResult("3. Multiple returns - no extra header on over-return", returnCountBeforeOverReturn, returnCountAfterOverReturn);
            AddResult("3. Multiple returns - no stock mutation on over-return", stockBeforeOverReturn, stockAfterOverReturn);
            AddResult("3. Multiple returns - no balance mutation on over-return", balanceBeforeOverReturn, balanceAfterOverReturn);
        }

        private static async Task RunWrongInvoiceItemAsync(string marker)
        {
            var invoiceA = await SeedInvoiceAsync(marker, "wrong-item-a", 100m, 0m, 0m, new[]
            {
                new InvoiceItemSeed("WRONG-A-MAT", 20m, 5m, 10m)
            });
            var invoiceB = await SeedInvoiceAsync(marker, "wrong-item-b", 100m, 0m, 0m, new[]
            {
                new InvoiceItemSeed("WRONG-B-MAT", 20m, 5m, 10m)
            });

            var balanceBefore = await GetClientBalanceAsync(invoiceA.ClientId);
            var stockBefore = await GetMaterialQuantityAsync(invoiceB.Items[0].MaterialId);
            var returnCountBefore = await CountReturnsForInvoiceAsync(invoiceA.InvoiceId);

            var result = await TrySendCommandAsync(new SalesReturnCreateModel
            {
                SalesInvoiceId = invoiceA.InvoiceId,
                Notes = $"{marker}: wrong invoice item",
                Items = new List<SalesReturnItemCreateModel>
                {
                    new SalesReturnItemCreateModel { SalesInvoiceItemId = invoiceB.Items[0].InvoiceItemId, ReturnedQuantity = 1m }
                }
            });

            AddResult("4. Wrong invoice item - fails", "failure", result.Success ? "success" : "failure");
            AddResult("4. Wrong invoice item - no return header", returnCountBefore, await CountReturnsForInvoiceAsync(invoiceA.InvoiceId));
            AddResult("4. Wrong invoice item - no stock mutation", stockBefore, await GetMaterialQuantityAsync(invoiceB.Items[0].MaterialId));
            AddResult("4. Wrong invoice item - no balance mutation", balanceBefore, await GetClientBalanceAsync(invoiceA.ClientId));
        }

        private static async Task RunSoftDeletedInvoiceAsync(string marker)
        {
            var seed = await SeedInvoiceAsync(marker, "soft-deleted", 100m, 0m, 0m, new[]
            {
                new InvoiceItemSeed("SOFT-MAT-1", 30m, 5m, 10m)
            });

            await using (var context = CreateContext())
            {
                var invoice = await context.SalesInvoices
                    .IgnoreQueryFilters()
                    .SingleAsync(i => i.Id == seed.InvoiceId);
                invoice.IsActive = false;
                await context.SaveChangesAsync();
            }

            var balanceBefore = await GetClientBalanceAsync(seed.ClientId);
            var stockBefore = await GetMaterialQuantityAsync(seed.Items[0].MaterialId);

            var result = await TrySendCommandAsync(new SalesReturnCreateModel
            {
                SalesInvoiceId = seed.InvoiceId,
                Notes = $"{marker}: soft deleted invoice",
                Items = new List<SalesReturnItemCreateModel>
                {
                    new SalesReturnItemCreateModel { SalesInvoiceItemId = seed.Items[0].InvoiceItemId, ReturnedQuantity = 1m }
                }
            });

            AddResult("5. Soft-deleted invoice - fails", "failure", result.Success ? "success" : "failure");
            AddResult("5. Soft-deleted invoice - no return header", 0, await CountReturnsForInvoiceAsync(seed.InvoiceId));
            AddResult("5. Soft-deleted invoice - no stock mutation", stockBefore, await GetMaterialQuantityAsync(seed.Items[0].MaterialId));
            AddResult("5. Soft-deleted invoice - no balance mutation", balanceBefore, await GetClientBalanceAsync(seed.ClientId));
        }

        private static async Task RunInactiveMaterialAsync(string marker)
        {
            var seed = await SeedInvoiceAsync(marker, "inactive-material", 100m, 0m, 0m, new[]
            {
                new InvoiceItemSeed("INACTIVE-MAT-1", 30m, 5m, 10m)
            });

            await using (var context = CreateContext())
            {
                var material = await context.Materials
                    .IgnoreQueryFilters()
                    .SingleAsync(m => m.Id == seed.Items[0].MaterialId);
                material.IsActive = false;
                await context.SaveChangesAsync();
            }

            var stockBefore = await GetMaterialQuantityIgnoringFiltersAsync(seed.Items[0].MaterialId);

            var result = await TrySendCommandAsync(new SalesReturnCreateModel
            {
                SalesInvoiceId = seed.InvoiceId,
                Notes = $"{marker}: inactive material return",
                Items = new List<SalesReturnItemCreateModel>
                {
                    new SalesReturnItemCreateModel { SalesInvoiceItemId = seed.Items[0].InvoiceItemId, ReturnedQuantity = 2m }
                }
            });

            AddResult("6. Inactive material - remains returnable", "success", result.Success ? "success" : result.Error);
            AddResult("6. Inactive material - stock restored through IgnoreQueryFilters", stockBefore + 2m, await GetMaterialQuantityIgnoringFiltersAsync(seed.Items[0].MaterialId));
            AddResult("6. Inactive material - active invoice still visible", 1, await CountReturnsForInvoiceAsync(seed.InvoiceId));
        }

        private static async Task RunRollbackProofAsync(string marker)
        {
            var seed = await SeedInvoiceAsync(marker, "rollback-proof", 300m, 0m, 0m, new[]
            {
                new InvoiceItemSeed("ROLLBACK-MAT-1", 50m, 5m, 20m),
                new InvoiceItemSeed("ROLLBACK-MAT-2", 50m, 5m, 20m)
            });

            var materialOneStockBefore = await GetMaterialQuantityAsync(seed.Items[0].MaterialId);
            var materialTwoStockBefore = await GetMaterialQuantityAsync(seed.Items[1].MaterialId);
            var balanceBefore = await GetClientBalanceAsync(seed.ClientId);
            var returnCountBefore = await CountReturnsForInvoiceAsync(seed.InvoiceId);

            var result = await TrySendCommandAsync(new SalesReturnCreateModel
            {
                SalesInvoiceId = seed.InvoiceId,
                Notes = $"{marker}: forced exception rollback proof",
                Items = new List<SalesReturnItemCreateModel>
                {
                    new SalesReturnItemCreateModel { SalesInvoiceItemId = seed.Items[0].InvoiceItemId, ReturnedQuantity = 2m },
                    new SalesReturnItemCreateModel { SalesInvoiceItemId = seed.Items[1].InvoiceItemId, ReturnedQuantity = 6m }
                }
            });

            AddResult("7. Forced exception - fails", "failure", result.Success ? "success" : "failure");
            AddResult("7. Forced exception - no return header", returnCountBefore, await CountReturnsForInvoiceAsync(seed.InvoiceId));
            AddResult("7. Forced exception - no return items", 0, await CountReturnItemsForInvoiceAsync(seed.InvoiceId));
            AddResult("7. Forced exception - material 1 stock unchanged", materialOneStockBefore, await GetMaterialQuantityAsync(seed.Items[0].MaterialId));
            AddResult("7. Forced exception - material 2 stock unchanged", materialTwoStockBefore, await GetMaterialQuantityAsync(seed.Items[1].MaterialId));
            AddResult("7. Forced exception - client balance unchanged", balanceBefore, await GetClientBalanceAsync(seed.ClientId));
        }

        private static async Task RunConcurrencyScenarioAsync(string marker)
        {
            var seed = await SeedInvoiceAsync(marker, "concurrency", 100m, 0m, 0m, new[]
            {
                new InvoiceItemSeed("CONCURRENCY-MAT-1", 50m, 5m, 20m)
            });

            var materialStockBefore = await GetMaterialQuantityAsync(seed.Items[0].MaterialId);
            var balanceBefore = await GetClientBalanceAsync(seed.ClientId);
            var returnCountBefore = await CountReturnsForInvoiceAsync(seed.InvoiceId);

            await using var context = CreateContext(new ForceClientConcurrencyInterceptor(ConnectionString));
            var handler = new CreateSalesReturnHandler(
                context,
                NullLogger<CreateSalesReturnHandler>.Instance);

            var result = await TryRunAsync(async () => await handler.Handle(new CreateSalesReturnCommand
            {
                Model = new SalesReturnCreateModel
                {
                    SalesInvoiceId = seed.InvoiceId,
                    Notes = $"{marker}: forced concurrency",
                    Items = new List<SalesReturnItemCreateModel>
                    {
                        new SalesReturnItemCreateModel { SalesInvoiceItemId = seed.Items[0].InvoiceItemId, ReturnedQuantity = 1m }
                    }
                }
            }, CancellationToken.None));

            AddResult("8. Concurrency - safe failure", "DbUpdateConcurrencyException", result.ExceptionType);
            AddResult("8. Concurrency - no return header", returnCountBefore, await CountReturnsForInvoiceAsync(seed.InvoiceId));
            AddResult("8. Concurrency - stock unchanged", materialStockBefore, await GetMaterialQuantityAsync(seed.Items[0].MaterialId));
            AddResult("8. Concurrency - balance unchanged", balanceBefore, await GetClientBalanceAsync(seed.ClientId));
        }

        private static async Task<SalesReturnViewModel> SendCommandAsync(SalesReturnCreateModel model)
        {
            await using var context = CreateContext();
            var handler = new CreateSalesReturnHandler(
                context,
                NullLogger<CreateSalesReturnHandler>.Instance);

            return await handler.Handle(new CreateSalesReturnCommand { Model = model }, CancellationToken.None);
        }

        private static async Task<CommandAttempt> TrySendCommandAsync(SalesReturnCreateModel model)
        {
            return await TryRunAsync(async () => await SendCommandAsync(model));
        }

        private static async Task<CommandAttempt> TryRunAsync(Func<Task<SalesReturnViewModel>> action)
        {
            try
            {
                var result = await action();
                return new CommandAttempt(true, null, null, result);
            }
            catch (Exception ex)
            {
                return new CommandAttempt(false, ex.Message, ex.GetType().Name, null);
            }
        }

        private static async Task<InvoiceSeedResult> SeedInvoiceAsync(
            string marker,
            string caseName,
            decimal initialClientBalance,
            decimal paidAmount,
            decimal discountAmount,
            IReadOnlyCollection<InvoiceItemSeed> itemSeeds)
        {
            await using var context = CreateContext();

            var client = new Client
            {
                Name = $"{marker}-{caseName}-client",
                Phone = CreatePhone(marker, caseName),
                Balance = initialClientBalance,
                Address = marker,
                IsActive = true
            };

            context.Clients.Add(client);

            var invoice = new SalesInvoice
            {
                InvoiceNumber = $"SAL-{marker}-{caseName}",
                InvoiceDate = DateTime.Now,
                Client = client,
                PaidAmount = paidAmount,
                DiscountAmount = discountAmount,
                IsActive = true,
                Notes = marker
            };

            decimal totalAmount = 0m;
            foreach (var itemSeed in itemSeeds)
            {
                var material = new Material
                {
                    Name = $"{marker}-{caseName}-{itemSeed.CodeSuffix}",
                    Code = $"{marker}-{caseName}-{itemSeed.CodeSuffix}",
                    Unit = "pcs",
                    Quantity = itemSeed.InitialStock,
                    ReservedQuantity = 0m,
                    SellingPrice = itemSeed.UnitPrice,
                    Description = marker,
                    IsActive = true
                };

                material.Quantity -= itemSeed.Quantity;
                totalAmount += itemSeed.Quantity * itemSeed.UnitPrice;

                invoice.SalesInvoiceItems.Add(new SalesInvoiceItem
                {
                    Material = material,
                    Quantity = itemSeed.Quantity,
                    UnitPrice = itemSeed.UnitPrice,
                    TotalPrice = itemSeed.Quantity * itemSeed.UnitPrice
                });
            }

            invoice.TotalAmount = totalAmount;
            invoice.RemainingAmount = totalAmount - discountAmount - paidAmount;
            client.Balance += invoice.RemainingAmount;

            context.SalesInvoices.Add(invoice);
            await context.SaveChangesAsync();

            return new InvoiceSeedResult(
                invoice.Id,
                client.Id,
                invoice.SalesInvoiceItems.Select(item => new InvoiceItemSeedResult(item.Id, item.MaterialId)).ToList());
        }

        private static async Task<ReturnSnapshot> GetReturnSnapshotAsync(int returnId)
        {
            await using var context = CreateContext();
            var salesReturn = await context.SalesReturns
                .IgnoreQueryFilters()
                .Include(r => r.SalesReturnItems)
                .SingleOrDefaultAsync(r => r.Id == returnId);

            if (salesReturn == null)
            {
                return ReturnSnapshot.Missing;
            }

            return new ReturnSnapshot(
                true,
                salesReturn.SalesReturnItems.Count,
                salesReturn.TotalNetAmount,
                salesReturn.TotalProratedDiscount,
                salesReturn.SalesReturnItems
                    .Select(item => new ReturnItemSnapshot(item.NetUnitPrice, item.TotalReturnNetAmount))
                    .ToList());
        }

        private static async Task<decimal> GetMaterialQuantityAsync(int materialId)
        {
            await using var context = CreateContext();
            return await context.Materials
                .Where(m => m.Id == materialId)
                .Select(m => m.Quantity)
                .SingleAsync();
        }

        private static async Task<decimal> GetMaterialQuantityIgnoringFiltersAsync(int materialId)
        {
            await using var context = CreateContext();
            return await context.Materials
                .IgnoreQueryFilters()
                .Where(m => m.Id == materialId)
                .Select(m => m.Quantity)
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

        private static async Task<int> CountReturnsForInvoiceAsync(int invoiceId)
        {
            await using var context = CreateContext();
            return await context.SalesReturns
                .IgnoreQueryFilters()
                .CountAsync(r => r.SalesInvoiceId == invoiceId);
        }

        private static async Task<int> CountReturnItemsForInvoiceAsync(int invoiceId)
        {
            await using var context = CreateContext();
            return await context.SalesReturnItems
                .IgnoreQueryFilters()
                .CountAsync(ri => ri.SalesReturn.SalesInvoiceId == invoiceId);
        }

        private static MaterialManagementContext CreateContext(params IInterceptor[] interceptors)
        {
            var builder = new DbContextOptionsBuilder<MaterialManagementContext>()
                .UseSqlServer(ConnectionString)
                .EnableSensitiveDataLogging();

            if (interceptors.Length > 0)
            {
                builder.AddInterceptors(interceptors);
            }

            return new MaterialManagementContext(builder.Options);
        }

        private static async Task CleanupAsync(string marker)
        {
            await using var context = CreateContext();

            var returnIds = await context.SalesReturns
                .IgnoreQueryFilters()
                .Where(r => r.ReturnNumber.Contains(marker) || r.Notes == marker || (r.Notes != null && r.Notes.Contains(marker)))
                .Select(r => r.Id)
                .ToListAsync();

            var invoiceIds = await context.SalesInvoices
                .IgnoreQueryFilters()
                .Where(i => i.InvoiceNumber.Contains(marker) || i.Notes == marker || (i.Notes != null && i.Notes.Contains(marker)))
                .Select(i => i.Id)
                .ToListAsync();

            var clientIds = await context.Clients
                .IgnoreQueryFilters()
                .Where(c => c.Address == marker || c.Name.Contains(marker))
                .Select(c => c.Id)
                .ToListAsync();

            var materialIds = await context.Materials
                .IgnoreQueryFilters()
                .Where(m => m.Description == marker || m.Code.Contains(marker))
                .Select(m => m.Id)
                .ToListAsync();

            if (returnIds.Count > 0)
            {
                var returns = await context.SalesReturns
                    .IgnoreQueryFilters()
                    .Where(r => returnIds.Contains(r.Id))
                    .ToListAsync();
                context.SalesReturns.RemoveRange(returns);
                await context.SaveChangesAsync();
            }

            if (invoiceIds.Count > 0)
            {
                var invoiceItems = await context.SalesInvoiceItems
                    .IgnoreQueryFilters()
                    .Where(i => invoiceIds.Contains(i.SalesInvoiceId))
                    .ToListAsync();
                context.SalesInvoiceItems.RemoveRange(invoiceItems);

                var invoices = await context.SalesInvoices
                    .IgnoreQueryFilters()
                    .Where(i => invoiceIds.Contains(i.Id))
                    .ToListAsync();
                context.SalesInvoices.RemoveRange(invoices);
                await context.SaveChangesAsync();
            }

            if (clientIds.Count > 0)
            {
                var clients = await context.Clients
                    .IgnoreQueryFilters()
                    .Where(c => clientIds.Contains(c.Id))
                    .ToListAsync();
                context.Clients.RemoveRange(clients);
            }

            if (materialIds.Count > 0)
            {
                var materials = await context.Materials
                    .IgnoreQueryFilters()
                    .Where(m => materialIds.Contains(m.Id))
                    .ToListAsync();
                context.Materials.RemoveRange(materials);
            }

            await context.SaveChangesAsync();
        }

        private static string CreatePhone(string marker, string caseName)
        {
            var hash = ((uint)HashCode.Combine(marker, caseName)).ToString();
            return $"9{hash}".PadRight(15, '0').Substring(0, 15);
        }

        private static void AddResult<T>(string caseName, T expected, T actual)
        {
            Results.Add(new TestResult(
                caseName,
                expected?.ToString() ?? string.Empty,
                actual?.ToString() ?? string.Empty,
                EqualityComparer<T>.Default.Equals(expected, actual)));
        }

        private sealed class ForceClientConcurrencyInterceptor : SaveChangesInterceptor
        {
            private readonly string _connectionString;
            private bool _hasRun;

            public ForceClientConcurrencyInterceptor(string connectionString)
            {
                _connectionString = connectionString;
            }

            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                if (!_hasRun && eventData.Context is MaterialManagementContext context)
                {
                    var clientId = context.ChangeTracker.Entries<Client>()
                        .Where(entry => entry.State == EntityState.Modified)
                        .Select(entry => entry.Entity.Id)
                        .FirstOrDefault();

                    if (clientId > 0)
                    {
                        _hasRun = true;
                        await using var externalContext = new MaterialManagementContext(
                            new DbContextOptionsBuilder<MaterialManagementContext>()
                                .UseSqlServer(_connectionString)
                                .Options);

                        await externalContext.Database.ExecuteSqlRawAsync(
                            "UPDATE [Clients] SET [Name] = [Name] WHERE [Id] = {0}",
                            new object[] { clientId },
                            cancellationToken);
                    }
                }

                return await base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }

        private sealed record TestResult(string CaseName, string Expected, string Actual, bool Passed);

        private sealed record InvoiceItemSeed(string CodeSuffix, decimal InitialStock, decimal Quantity, decimal UnitPrice);

        private sealed record InvoiceItemSeedResult(int InvoiceItemId, int MaterialId);

        private sealed record InvoiceSeedResult(int InvoiceId, int ClientId, IReadOnlyList<InvoiceItemSeedResult> Items);

        private sealed record ReturnItemSnapshot(decimal NetUnitPrice, decimal TotalReturnNetAmount);

        private sealed record ReturnSnapshot(
            bool ReturnExists,
            int ItemCount,
            decimal TotalNetAmount,
            decimal TotalProratedDiscount,
            IReadOnlyList<ReturnItemSnapshot> Items)
        {
            public static readonly ReturnSnapshot Missing = new(false, 0, 0m, 0m, Array.Empty<ReturnItemSnapshot>());
        }

        private sealed record CommandAttempt(
            bool Success,
            string? Error,
            string? ExceptionType,
            SalesReturnViewModel? Result);
    }
}