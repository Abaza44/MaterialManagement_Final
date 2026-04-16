using AutoMapper;
using MaterialManagement.BLL.Helper;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.Service.Implementations;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Implementations;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTestSandbox
{
    internal static class Phase2PurchaseInvoiceVerification
    {
        private const string ConnectionString = "Server=.;Database=MaterialManagementDB;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";

        private static readonly List<TestResult> Results = new();
        private static readonly IMapper Mapper = CreateMapper();

        public static async Task<int> RunAsync()
        {
            var marker = $"P2PI{DateTime.UtcNow:HHmmssfff}";
            Results.Clear();
            Console.WriteLine($"Starting Phase 2 Part A purchase invoice verification. Marker={marker}");

            try
            {
                await CleanupAsync(marker);

                await RunValidSupplierPurchaseCreateAsync(marker);
                await RunInvalidModeCreateChecksAsync(marker);
                await RunValidClientReturnCreateAsync(marker);
                await RunSupplierPurchaseDeleteAsync(marker);
                await RunClientReturnDeleteAsync(marker);
                await RunDeleteBlockedByNegativeStockAsync(marker);
                await RunDeleteBlockedByReservedStockAsync(marker);
                await RunInvalidPersistedModeDeleteChecksAsync(marker);
            }
            finally
            {
                await CleanupAsync(marker);
            }

            Console.WriteLine();
            Console.WriteLine("=== Phase 2 Part A PurchaseInvoice Verification Results ===");
            foreach (var result in Results)
            {
                Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} | {result.CaseName} | Expected: {result.Expected} | Actual: {result.Actual}");
            }

            var failedCount = Results.Count(result => !result.Passed);
            Console.WriteLine($"SUMMARY: {Results.Count - failedCount}/{Results.Count} checks passed.");
            return failedCount == 0 ? 0 : 1;
        }

        private static async Task RunValidSupplierPurchaseCreateAsync(string marker)
        {
            var supplierId = await SeedSupplierAsync(marker, "supplier-create", 100m);
            var materialId = await SeedMaterialAsync(marker, "supplier-create-material", 10m, 25m, 0m);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
                {
                    SupplierId = supplierId,
                    ClientId = null,
                    DiscountAmount = 20m,
                    PaidAmount = 30m,
                    Items = new List<PurchaseInvoiceItemCreateModel>
                    {
                        new PurchaseInvoiceItemCreateModel { MaterialId = materialId, Quantity = 4m, UnitPrice = 25m }
                    }
                });
            });

            var invoice = await GetLatestPurchaseInvoiceForSupplierAsync(supplierId);
            var material = await GetMaterialSnapshotAsync(materialId);
            AddResult("1. Supplier purchase create - succeeds", "success", result.Success ? "success" : result.Message);
            AddResult("1. Supplier purchase create - stores supplier only", "supplier-only", invoice.SupplierId == supplierId && invoice.ClientId == null ? "supplier-only" : "mixed");
            AddResult("1. Supplier purchase create - net total", 80m, invoice.TotalAmount);
            AddResult("1. Supplier purchase create - remaining", 50m, invoice.RemainingAmount);
            AddResult("1. Supplier purchase create - supplier balance", 150m, await GetSupplierBalanceAsync(supplierId));
            AddResult("1. Supplier purchase create - stock increased", 14m, material.Quantity);
            AddResult("1. Supplier purchase create - purchase price updated", 25m, material.PurchasePrice ?? 0m);
        }

        private static async Task RunInvalidModeCreateChecksAsync(string marker)
        {
            await ExpectCreateFailureAsync(marker, "missing-mode", "missing supplier/client mode", null, null, 0m, 0m);

            var supplierId = await SeedSupplierAsync(marker, "mixed-mode-supplier", 0m);
            var clientId = await SeedClientAsync(marker, "mixed-mode-client", 0m);
            await ExpectCreateFailureAsync(marker, "mixed-mode", "mixed supplier/client mode", supplierId, clientId, 0m, 0m);

            var paidSupplierId = await SeedSupplierAsync(marker, "paid-over-net-supplier", 0m);
            await ExpectCreateFailureAsync(marker, "paid-over-net", "paid > net due", paidSupplierId, null, 10m, 91m);
        }

        private static async Task ExpectCreateFailureAsync(
            string marker,
            string suffix,
            string caseName,
            int? supplierId,
            int? clientId,
            decimal discount,
            decimal paid)
        {
            var materialId = await SeedMaterialAsync(marker, suffix + "-material", 10m, 15m, 0m);
            var stockBefore = await GetMaterialQuantityAsync(materialId);
            var supplierBalanceBefore = supplierId.HasValue ? await GetSupplierBalanceAsync(supplierId.Value) : 0m;
            var clientBalanceBefore = clientId.HasValue ? await GetClientBalanceAsync(clientId.Value) : 0m;
            var countBefore = await CountPurchaseInvoicesForMarkerAsync(marker);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
                {
                    SupplierId = supplierId,
                    ClientId = clientId,
                    DiscountAmount = discount,
                    PaidAmount = paid,
                    Items = new List<PurchaseInvoiceItemCreateModel>
                    {
                        new PurchaseInvoiceItemCreateModel { MaterialId = materialId, Quantity = 1m, UnitPrice = 100m }
                    }
                });
            });

            AddResult($"2. Create {caseName} - fails", "failure", result.Success ? "success" : "failure");
            AddResult($"2. Create {caseName} - no invoice persisted", countBefore, await CountPurchaseInvoicesForMarkerAsync(marker));
            AddResult($"2. Create {caseName} - stock unchanged", stockBefore, await GetMaterialQuantityAsync(materialId));
            if (supplierId.HasValue)
                AddResult($"2. Create {caseName} - supplier balance unchanged", supplierBalanceBefore, await GetSupplierBalanceAsync(supplierId.Value));
            if (clientId.HasValue)
                AddResult($"2. Create {caseName} - client balance unchanged", clientBalanceBefore, await GetClientBalanceAsync(clientId.Value));
        }

        private static async Task RunValidClientReturnCreateAsync(string marker)
        {
            var clientId = await SeedClientAsync(marker, "client-return", 300m);
            var materialId = await SeedMaterialAsync(marker, "client-return-material", 10m, 40m, 12m);
            var purchasePriceBefore = (await GetMaterialSnapshotAsync(materialId)).PurchasePrice ?? 0m;

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
                {
                    SupplierId = null,
                    ClientId = clientId,
                    DiscountAmount = 20m,
                    PaidAmount = 30m,
                    Items = new List<PurchaseInvoiceItemCreateModel>
                    {
                        new PurchaseInvoiceItemCreateModel { MaterialId = materialId, Quantity = 4m, UnitPrice = 25m }
                    }
                });
            });

            var invoice = await GetLatestPurchaseInvoiceForClientAsync(clientId);
            var material = await GetMaterialSnapshotAsync(materialId);
            AddResult("3. Client return create - succeeds", "success", result.Success ? "success" : result.Message);
            AddResult("3. Client return create - stores client only", "client-only", invoice.ClientId == clientId && invoice.SupplierId == null ? "client-only" : "mixed");
            AddResult("3. Client return create - net total", 80m, invoice.TotalAmount);
            AddResult("3. Client return create - remaining", 50m, invoice.RemainingAmount);
            AddResult("3. Client return create - client balance uses remaining", 250m, await GetClientBalanceAsync(clientId));
            AddResult("3. Client return create - stock increased", 14m, material.Quantity);
            AddResult("3. Client return create - purchase price unchanged", purchasePriceBefore, material.PurchasePrice ?? 0m);
        }

        private static async Task RunSupplierPurchaseDeleteAsync(string marker)
        {
            var seed = await CreateSupplierPurchaseAsync(marker, "supplier-delete", 100m, 20m, 5m, 10m, 10m, 20m);
            var deleteResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).DeleteInvoiceAsync(seed.InvoiceId);
            });

            AddResult("4. Supplier purchase delete - succeeds", "success", deleteResult.Success ? "success" : deleteResult.Message);
            AddResult("4. Supplier purchase delete - soft deleted", false, await IsPurchaseInvoiceActiveAsync(seed.InvoiceId));
            AddResult("4. Supplier purchase delete - stock reversed", 20m, await GetMaterialQuantityAsync(seed.MaterialId));
            AddResult("4. Supplier purchase delete - supplier balance reversed", 100m, await GetSupplierBalanceAsync(seed.SupplierId!.Value));
        }

        private static async Task RunClientReturnDeleteAsync(string marker)
        {
            var seed = await CreateClientReturnAsync(marker, "client-return-delete", 300m, 20m, 5m, 10m, 10m, 20m);
            var deleteResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).DeleteInvoiceAsync(seed.InvoiceId);
            });

            AddResult("5. Client return delete - succeeds", "success", deleteResult.Success ? "success" : deleteResult.Message);
            AddResult("5. Client return delete - soft deleted", false, await IsPurchaseInvoiceActiveAsync(seed.InvoiceId));
            AddResult("5. Client return delete - stock reversed", 20m, await GetMaterialQuantityAsync(seed.MaterialId));
            AddResult("5. Client return delete - client balance reversed by remaining", 300m, await GetClientBalanceAsync(seed.ClientId!.Value));
        }

        private static async Task RunDeleteBlockedByNegativeStockAsync(string marker)
        {
            var seed = await CreateSupplierPurchaseAsync(marker, "delete-negative-stock", 100m, 0m, 5m, 10m, 0m, 0m);
            await SetMaterialQuantityAsync(seed.MaterialId, 2m, 0m);
            var balanceBefore = await GetSupplierBalanceAsync(seed.SupplierId!.Value);

            var deleteResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).DeleteInvoiceAsync(seed.InvoiceId);
            });

            AddResult("6. Delete blocked by negative stock - fails", "failure", deleteResult.Success ? "success" : "failure");
            AddResult("6. Delete blocked by negative stock - invoice remains active", true, await IsPurchaseInvoiceActiveAsync(seed.InvoiceId));
            AddResult("6. Delete blocked by negative stock - stock unchanged", 2m, await GetMaterialQuantityAsync(seed.MaterialId));
            AddResult("6. Delete blocked by negative stock - supplier balance unchanged", balanceBefore, await GetSupplierBalanceAsync(seed.SupplierId.Value));
        }

        private static async Task RunDeleteBlockedByReservedStockAsync(string marker)
        {
            var seed = await CreateSupplierPurchaseAsync(marker, "delete-reserved-stock", 100m, 10m, 5m, 10m, 0m, 0m);
            await SetMaterialQuantityAsync(seed.MaterialId, 12m, 10m);
            var balanceBefore = await GetSupplierBalanceAsync(seed.SupplierId!.Value);

            var deleteResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).DeleteInvoiceAsync(seed.InvoiceId);
            });

            var material = await GetMaterialSnapshotAsync(seed.MaterialId);
            AddResult("7. Delete blocked by reserved stock - fails", "failure", deleteResult.Success ? "success" : "failure");
            AddResult("7. Delete blocked by reserved stock - invoice remains active", true, await IsPurchaseInvoiceActiveAsync(seed.InvoiceId));
            AddResult("7. Delete blocked by reserved stock - stock unchanged", 12m, material.Quantity);
            AddResult("7. Delete blocked by reserved stock - reserved unchanged", 10m, material.ReservedQuantity);
            AddResult("7. Delete blocked by reserved stock - supplier balance unchanged", balanceBefore, await GetSupplierBalanceAsync(seed.SupplierId.Value));
        }

        private static async Task RunInvalidPersistedModeDeleteChecksAsync(string marker)
        {
            var mixed = await SeedPersistedPurchaseInvoiceAsync(marker, "persisted-mixed", includeSupplier: true, includeClient: true);
            await ExpectInvalidPersistedDeleteFailureAsync("8. Invalid persisted mixed mode delete", mixed);

            var neither = await SeedPersistedPurchaseInvoiceAsync(marker, "persisted-neither", includeSupplier: false, includeClient: false);
            await ExpectInvalidPersistedDeleteFailureAsync("8. Invalid persisted neither mode delete", neither);
        }

        private static async Task ExpectInvalidPersistedDeleteFailureAsync(string caseName, InvoiceSeed seed)
        {
            var stockBefore = await GetMaterialQuantityAsync(seed.MaterialId);
            var supplierBalanceBefore = seed.SupplierId.HasValue ? await GetSupplierBalanceAsync(seed.SupplierId.Value) : 0m;
            var clientBalanceBefore = seed.ClientId.HasValue ? await GetClientBalanceAsync(seed.ClientId.Value) : 0m;

            var deleteResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateService(context).DeleteInvoiceAsync(seed.InvoiceId);
            });

            AddResult($"{caseName} - fails", "failure", deleteResult.Success ? "success" : "failure");
            AddResult($"{caseName} - invoice remains active", true, await IsPurchaseInvoiceActiveAsync(seed.InvoiceId));
            AddResult($"{caseName} - stock unchanged", stockBefore, await GetMaterialQuantityAsync(seed.MaterialId));
            if (seed.SupplierId.HasValue)
                AddResult($"{caseName} - supplier balance unchanged", supplierBalanceBefore, await GetSupplierBalanceAsync(seed.SupplierId.Value));
            if (seed.ClientId.HasValue)
                AddResult($"{caseName} - client balance unchanged", clientBalanceBefore, await GetClientBalanceAsync(seed.ClientId.Value));
        }

        private static async Task<InvoiceSeed> CreateSupplierPurchaseAsync(
            string marker,
            string suffix,
            decimal supplierBalance,
            decimal initialStock,
            decimal quantity,
            decimal unitPrice,
            decimal discount,
            decimal paid)
        {
            var supplierId = await SeedSupplierAsync(marker, suffix + "-supplier", supplierBalance);
            var materialId = await SeedMaterialAsync(marker, suffix + "-material", initialStock, unitPrice, 0m);

            await using var context = CreateContext();
            await CreateService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
            {
                SupplierId = supplierId,
                DiscountAmount = discount,
                PaidAmount = paid,
                Items = new List<PurchaseInvoiceItemCreateModel>
                {
                    new PurchaseInvoiceItemCreateModel { MaterialId = materialId, Quantity = quantity, UnitPrice = unitPrice }
                }
            });

            var invoice = await GetLatestPurchaseInvoiceForSupplierAsync(supplierId);
            return new InvoiceSeed(invoice.Id, supplierId, null, materialId);
        }

        private static async Task<InvoiceSeed> CreateClientReturnAsync(
            string marker,
            string suffix,
            decimal clientBalance,
            decimal initialStock,
            decimal quantity,
            decimal unitPrice,
            decimal discount,
            decimal paid)
        {
            var clientId = await SeedClientAsync(marker, suffix + "-client", clientBalance);
            var materialId = await SeedMaterialAsync(marker, suffix + "-material", initialStock, unitPrice, 0m);

            await using var context = CreateContext();
            await CreateService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
            {
                ClientId = clientId,
                DiscountAmount = discount,
                PaidAmount = paid,
                Items = new List<PurchaseInvoiceItemCreateModel>
                {
                    new PurchaseInvoiceItemCreateModel { MaterialId = materialId, Quantity = quantity, UnitPrice = unitPrice }
                }
            });

            var invoice = await GetLatestPurchaseInvoiceForClientAsync(clientId);
            return new InvoiceSeed(invoice.Id, null, clientId, materialId);
        }

        private static async Task<InvoiceSeed> SeedPersistedPurchaseInvoiceAsync(
            string marker,
            string suffix,
            bool includeSupplier,
            bool includeClient)
        {
            await using var context = CreateContext();

            var material = CreateMaterial(marker, suffix + "-material", 20m, 10m, 0m);
            context.Materials.Add(material);

            Supplier? supplier = null;
            if (includeSupplier)
            {
                supplier = CreateSupplier(marker, suffix + "-supplier", 100m);
                context.Suppliers.Add(supplier);
            }

            Client? client = null;
            if (includeClient)
            {
                client = CreateClient(marker, suffix + "-client", 100m);
                context.Clients.Add(client);
            }

            var invoice = new PurchaseInvoice
            {
                InvoiceNumber = $"PUR-{marker}-{suffix}",
                InvoiceDate = DateTime.Now,
                TotalAmount = 20m,
                DiscountAmount = 0m,
                PaidAmount = 0m,
                RemainingAmount = 20m,
                Notes = marker,
                IsActive = true,
                PurchaseInvoiceItems = new List<PurchaseInvoiceItem>
                {
                    new PurchaseInvoiceItem
                    {
                        Material = material,
                        Quantity = 2m,
                        UnitPrice = 10m,
                        TotalPrice = 20m
                    }
                }
            };

            if (supplier != null)
                invoice.Supplier = supplier;

            if (client != null)
                invoice.Client = client;

            context.PurchaseInvoices.Add(invoice);
            await context.SaveChangesAsync();

            return new InvoiceSeed(invoice.Id, supplier?.Id, client?.Id, material.Id);
        }

        private static PurchaseInvoiceService CreateService(MaterialManagementContext context)
        {
            return new PurchaseInvoiceService(
                new PurchaseInvoiceRepo(context),
                new MaterialRepo(context),
                new SupplierRepo(context),
                new ClientRepo(context),
                context,
                Mapper);
        }

        private static async Task<int> SeedSupplierAsync(string marker, string suffix, decimal balance)
        {
            await using var context = CreateContext();
            var supplier = CreateSupplier(marker, suffix, balance);
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            return supplier.Id;
        }

        private static async Task<int> SeedClientAsync(string marker, string suffix, decimal balance)
        {
            await using var context = CreateContext();
            var client = CreateClient(marker, suffix, balance);
            context.Clients.Add(client);
            await context.SaveChangesAsync();
            return client.Id;
        }

        private static async Task<int> SeedMaterialAsync(
            string marker,
            string suffix,
            decimal quantity,
            decimal price,
            decimal reservedQuantity)
        {
            await using var context = CreateContext();
            var material = CreateMaterial(marker, suffix, quantity, price, reservedQuantity);
            context.Materials.Add(material);
            await context.SaveChangesAsync();
            return material.Id;
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

        private static Material CreateMaterial(
            string marker,
            string suffix,
            decimal quantity,
            decimal price,
            decimal reservedQuantity)
        {
            return new Material
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
        }

        private static async Task<PurchaseInvoice> GetLatestPurchaseInvoiceForSupplierAsync(int supplierId)
        {
            await using var context = CreateContext();
            return await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(invoice => invoice.SupplierId == supplierId)
                .OrderByDescending(invoice => invoice.Id)
                .SingleAsync();
        }

        private static async Task<PurchaseInvoice> GetLatestPurchaseInvoiceForClientAsync(int clientId)
        {
            await using var context = CreateContext();
            return await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(invoice => invoice.ClientId == clientId)
                .OrderByDescending(invoice => invoice.Id)
                .SingleAsync();
        }

        private static async Task<decimal> GetSupplierBalanceAsync(int supplierId)
        {
            await using var context = CreateContext();
            return await context.Suppliers
                .IgnoreQueryFilters()
                .Where(supplier => supplier.Id == supplierId)
                .Select(supplier => supplier.Balance)
                .SingleAsync();
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

        private static async Task<decimal> GetMaterialQuantityAsync(int materialId)
        {
            return (await GetMaterialSnapshotAsync(materialId)).Quantity;
        }

        private static async Task<MaterialSnapshot> GetMaterialSnapshotAsync(int materialId)
        {
            await using var context = CreateContext();
            return await context.Materials
                .IgnoreQueryFilters()
                .Where(material => material.Id == materialId)
                .Select(material => new MaterialSnapshot(
                    material.Quantity,
                    material.ReservedQuantity,
                    material.PurchasePrice))
                .SingleAsync();
        }

        private static async Task SetMaterialQuantityAsync(int materialId, decimal quantity, decimal reservedQuantity)
        {
            await using var context = CreateContext();
            var material = await context.Materials
                .IgnoreQueryFilters()
                .SingleAsync(material => material.Id == materialId);
            material.Quantity = quantity;
            material.ReservedQuantity = reservedQuantity;
            await context.SaveChangesAsync();
        }

        private static async Task<bool> IsPurchaseInvoiceActiveAsync(int invoiceId)
        {
            await using var context = CreateContext();
            return await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(invoice => invoice.Id == invoiceId)
                .Select(invoice => invoice.IsActive)
                .SingleAsync();
        }

        private static async Task<int> CountPurchaseInvoicesForMarkerAsync(string marker)
        {
            await using var context = CreateContext();
            return await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .CountAsync(invoice => invoice.InvoiceNumber.Contains(marker) || invoice.Notes == marker);
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

            var purchaseInvoiceIds = await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(invoice =>
                    invoice.InvoiceNumber.Contains(marker) ||
                    invoice.Notes == marker ||
                    (invoice.SupplierId.HasValue && supplierIds.Contains(invoice.SupplierId.Value)) ||
                    (invoice.ClientId.HasValue && clientIds.Contains(invoice.ClientId.Value)))
                .Select(invoice => invoice.Id)
                .ToListAsync();

            var supplierPayments = await context.SupplierPayments
                .Where(payment =>
                    supplierIds.Contains(payment.SupplierId) ||
                    (payment.PurchaseInvoiceId.HasValue && purchaseInvoiceIds.Contains(payment.PurchaseInvoiceId.Value)))
                .ToListAsync();
            context.SupplierPayments.RemoveRange(supplierPayments);

            var purchaseInvoiceItems = await context.PurchaseInvoiceItems
                .IgnoreQueryFilters()
                .Where(item => purchaseInvoiceIds.Contains(item.PurchaseInvoiceId))
                .ToListAsync();
            context.PurchaseInvoiceItems.RemoveRange(purchaseInvoiceItems);

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

        private static MaterialManagementContext CreateContext()
        {
            var builder = new DbContextOptionsBuilder<MaterialManagementContext>()
                .UseSqlServer(ConnectionString)
                .EnableSensitiveDataLogging();

            return new MaterialManagementContext(builder.Options);
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
            return $"7{hash}".PadRight(15, '0').Substring(0, 15);
        }

        private static string CreateMaterialCode(string marker, string suffix)
        {
            var normalizedSuffix = suffix.Length <= 28 ? suffix : suffix.Substring(0, 28);
            return $"{marker}-{normalizedSuffix}";
        }

        private sealed record TestResult(string CaseName, string Expected, string Actual, bool Passed);

        private sealed record CommandAttempt(bool Success, string Message);

        private sealed record InvoiceSeed(int InvoiceId, int? SupplierId, int? ClientId, int MaterialId);

        private sealed record MaterialSnapshot(decimal Quantity, decimal ReservedQuantity, decimal? PurchasePrice);

    }
}
