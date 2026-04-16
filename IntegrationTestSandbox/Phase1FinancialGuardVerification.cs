using AutoMapper;
using MaterialManagement.BLL.Helper;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.ModelVM.Payment;
using MaterialManagement.BLL.Service.Implementations;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Implementations;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTestSandbox
{
    internal static class Phase1FinancialGuardVerification
    {
        private const string ConnectionString = "Server=.;Database=MaterialManagementDB;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";

        private static readonly List<TestResult> Results = new();
        private static readonly IMapper Mapper = CreateMapper();

        public static async Task<int> RunAsync()
        {
            var marker = $"P1{DateTime.UtcNow:HHmmssfff}";
            Results.Clear();
            Console.WriteLine($"Starting Phase 1 financial guard runtime verification. Marker={marker}");

            try
            {
                await CleanupAsync(marker);

                await RunValidSalesInvoiceCreateAsync(marker);
                await RunInvalidSalesInvoiceCreatesAsync(marker);
                await RunClientPaymentChecksAsync(marker);
                await RunSupplierPaymentChecksAsync(marker);
                await RunValidPurchaseInvoiceCreateAsync(marker);
                await RunInvalidPurchaseInvoiceCreatesAsync(marker);
            }
            finally
            {
                await CleanupAsync(marker);
            }

            Console.WriteLine();
            Console.WriteLine("=== Phase 1 Financial Guard Runtime Verification Results ===");
            foreach (var result in Results)
            {
                Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} | {result.CaseName} | Expected: {result.Expected} | Actual: {result.Actual}");
            }

            var failedCount = Results.Count(result => !result.Passed);
            Console.WriteLine($"SUMMARY: {Results.Count - failedCount}/{Results.Count} checks passed.");
            return failedCount == 0 ? 0 : 1;
        }

        private static async Task RunValidSalesInvoiceCreateAsync(string marker)
        {
            var clientId = await SeedClientAsync(marker, "sales-valid-client", 100m);
            var materialId = await SeedMaterialAsync(marker, "sales-valid-material", 25m, 100m);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateSalesInvoiceService(context).CreateInvoiceAsync(new SalesInvoiceCreateModel
                {
                    ClientId = clientId,
                    DiscountAmount = 20m,
                    PaidAmount = 150m,
                    Items = new List<SalesInvoiceItemCreateModel>
                    {
                        new SalesInvoiceItemCreateModel { MaterialId = materialId, Quantity = 3m, UnitPrice = 100m }
                    }
                });
            });

            var invoice = await GetLatestSalesInvoiceForClientAsync(clientId);
            AddResult("1. Sales invoice valid create - succeeds", "success", result.Success ? "success" : result.Message);
            AddResult("1. Sales invoice valid create - gross", 300m, invoice.TotalAmount);
            AddResult("1. Sales invoice valid create - discount", 20m, invoice.DiscountAmount);
            AddResult("1. Sales invoice valid create - paid", 150m, invoice.PaidAmount);
            AddResult("1. Sales invoice valid create - remaining", 130m, invoice.RemainingAmount);
            AddResult("1. Sales invoice valid create - client balance", 230m, await GetClientBalanceAsync(clientId));
            AddResult("1. Sales invoice valid create - stock", 22m, await GetMaterialQuantityAsync(materialId));
        }

        private static async Task RunInvalidSalesInvoiceCreatesAsync(string marker)
        {
            await ExpectSalesInvoiceFailureAsync(marker, "sales-neg-quantity", "negative quantity", -1m, 100m, 0m, 0m);
            await ExpectSalesInvoiceFailureAsync(marker, "sales-neg-price", "negative unit price", 1m, -100m, 0m, 0m);
            await ExpectSalesInvoiceFailureAsync(marker, "sales-neg-discount", "negative discount", 1m, 100m, -1m, 0m);
            await ExpectSalesInvoiceFailureAsync(marker, "sales-neg-paid", "negative paid amount", 1m, 100m, 0m, -1m);
            await ExpectSalesInvoiceFailureAsync(marker, "sales-paid-over-net", "paid > net due", 1m, 100m, 10m, 91m);
            await ExpectSalesInvoiceFailureAsync(marker, "sales-discount-over-gross", "discount > gross total", 1m, 100m, 101m, 0m);
        }

        private static async Task ExpectSalesInvoiceFailureAsync(
            string marker,
            string suffix,
            string caseName,
            decimal quantity,
            decimal unitPrice,
            decimal discount,
            decimal paid)
        {
            var clientId = await SeedClientAsync(marker, suffix + "-client", 0m);
            var materialId = await SeedMaterialAsync(marker, suffix + "-material", 10m, 100m);
            var stockBefore = await GetMaterialQuantityAsync(materialId);
            var balanceBefore = await GetClientBalanceAsync(clientId);
            var countBefore = await CountSalesInvoicesForClientAsync(clientId);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateSalesInvoiceService(context).CreateInvoiceAsync(new SalesInvoiceCreateModel
                {
                    ClientId = clientId,
                    DiscountAmount = discount,
                    PaidAmount = paid,
                    Items = new List<SalesInvoiceItemCreateModel>
                    {
                        new SalesInvoiceItemCreateModel { MaterialId = materialId, Quantity = quantity, UnitPrice = unitPrice }
                    }
                });
            });

            AddResult($"2. Sales invoice {caseName} - fails", "failure", result.Success ? "success" : "failure");
            AddResult($"2. Sales invoice {caseName} - no invoice persisted", countBefore, await CountSalesInvoicesForClientAsync(clientId));
            AddResult($"2. Sales invoice {caseName} - stock unchanged", stockBefore, await GetMaterialQuantityAsync(materialId));
            AddResult($"2. Sales invoice {caseName} - balance unchanged", balanceBefore, await GetClientBalanceAsync(clientId));
        }

        private static async Task RunClientPaymentChecksAsync(string marker)
        {
            var validSeed = await CreateSalesInvoiceSeedAsync(marker, "client-payment-valid", 0m, 2m, 100m, 20m, 50m);
            var validResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateClientPaymentService(context).AddPaymentAsync(new ClientPaymentCreateModel
                {
                    ClientId = validSeed.ClientId,
                    SalesInvoiceId = validSeed.InvoiceId,
                    Amount = 30m,
                    PaymentMethod = "cash",
                    Notes = marker
                });
            });

            var validInvoice = await GetSalesInvoiceAsync(validSeed.InvoiceId);
            AddResult("3. Client payment valid discounted invoice - succeeds", "success", validResult.Success ? "success" : validResult.Message);
            AddResult("3. Client payment valid discounted invoice - paid", 80m, validInvoice.PaidAmount);
            AddResult("3. Client payment valid discounted invoice - remaining", 100m, validInvoice.RemainingAmount);
            AddResult("3. Client payment valid discounted invoice - client balance", 100m, await GetClientBalanceAsync(validSeed.ClientId));

            var wrongSeed = await CreateSalesInvoiceSeedAsync(marker, "client-payment-wrong-owner", 0m, 1m, 100m, 0m, 0m);
            var wrongClientId = await SeedClientAsync(marker, "client-payment-wrong-owner-other", 75m);
            var wrongResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateClientPaymentService(context).AddPaymentAsync(new ClientPaymentCreateModel
                {
                    ClientId = wrongClientId,
                    SalesInvoiceId = wrongSeed.InvoiceId,
                    Amount = 10m,
                    PaymentMethod = "cash",
                    Notes = marker
                });
            });
            AddResult("3. Client payment wrong invoice ownership - fails", "failure", wrongResult.Success ? "success" : "failure");
            AddResult("3. Client payment wrong invoice ownership - other client balance unchanged", 75m, await GetClientBalanceAsync(wrongClientId));

            var overSeed = await CreateSalesInvoiceSeedAsync(marker, "client-payment-overpayment", 0m, 1m, 100m, 0m, 20m);
            var overResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateClientPaymentService(context).AddPaymentAsync(new ClientPaymentCreateModel
                {
                    ClientId = overSeed.ClientId,
                    SalesInvoiceId = overSeed.InvoiceId,
                    Amount = 81m,
                    PaymentMethod = "cash",
                    Notes = marker
                });
            });
            AddResult("3. Client payment greater than remaining - fails", "failure", overResult.Success ? "success" : "failure");
            AddResult("3. Client payment greater than remaining - remaining unchanged", 80m, (await GetSalesInvoiceAsync(overSeed.InvoiceId)).RemainingAmount);

            var paidSeed = await CreateSalesInvoiceSeedAsync(marker, "client-payment-fully-paid", 0m, 1m, 100m, 10m, 90m);
            var fullyPaidResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateClientPaymentService(context).AddPaymentAsync(new ClientPaymentCreateModel
                {
                    ClientId = paidSeed.ClientId,
                    SalesInvoiceId = paidSeed.InvoiceId,
                    Amount = 1m,
                    PaymentMethod = "cash",
                    Notes = marker
                });
            });
            AddResult("3. Client payment against fully paid invoice - fails", "failure", fullyPaidResult.Success ? "success" : "failure");

            var unallocatedClientId = await SeedClientAsync(marker, "client-payment-unallocated", 50m);
            var unallocatedResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateClientPaymentService(context).AddPaymentAsync(new ClientPaymentCreateModel
                {
                    ClientId = unallocatedClientId,
                    Amount = 51m,
                    PaymentMethod = "cash",
                    Notes = marker
                });
            });
            AddResult("3. Client unallocated payment above balance - fails", "failure", unallocatedResult.Success ? "success" : "failure");
            AddResult("3. Client unallocated payment above balance - balance unchanged", 50m, await GetClientBalanceAsync(unallocatedClientId));
        }

        private static async Task RunSupplierPaymentChecksAsync(string marker)
        {
            var validSeed = await CreatePurchaseInvoiceSeedAsync(marker, "supplier-payment-valid", 0m, 2m, 100m, 20m, 50m);
            var validResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateSupplierPaymentService(context).AddPaymentAsync(new SupplierPaymentCreateModel
                {
                    SupplierId = validSeed.SupplierId,
                    PurchaseInvoiceId = validSeed.InvoiceId,
                    Amount = 30m,
                    PaymentMethod = "cash",
                    Notes = marker
                });
            });

            var validInvoice = await GetPurchaseInvoiceAsync(validSeed.InvoiceId);
            AddResult("4. Supplier payment valid - succeeds", "success", validResult.Success ? "success" : validResult.Message);
            AddResult("4. Supplier payment valid - paid", 80m, validInvoice.PaidAmount);
            AddResult("4. Supplier payment valid - remaining", 100m, validInvoice.RemainingAmount);
            AddResult("4. Supplier payment valid - supplier balance", 100m, await GetSupplierBalanceAsync(validSeed.SupplierId));

            var wrongSeed = await CreatePurchaseInvoiceSeedAsync(marker, "supplier-payment-wrong-owner", 0m, 1m, 100m, 0m, 0m);
            var wrongSupplierId = await SeedSupplierAsync(marker, "supplier-payment-wrong-owner-other", 90m);
            var wrongResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateSupplierPaymentService(context).AddPaymentAsync(new SupplierPaymentCreateModel
                {
                    SupplierId = wrongSupplierId,
                    PurchaseInvoiceId = wrongSeed.InvoiceId,
                    Amount = 10m,
                    PaymentMethod = "cash",
                    Notes = marker
                });
            });
            AddResult("4. Supplier payment wrong invoice ownership - fails", "failure", wrongResult.Success ? "success" : "failure");
            AddResult("4. Supplier payment wrong invoice ownership - other supplier balance unchanged", 90m, await GetSupplierBalanceAsync(wrongSupplierId));

            var overSeed = await CreatePurchaseInvoiceSeedAsync(marker, "supplier-payment-overpayment", 0m, 1m, 100m, 0m, 20m);
            var overResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateSupplierPaymentService(context).AddPaymentAsync(new SupplierPaymentCreateModel
                {
                    SupplierId = overSeed.SupplierId,
                    PurchaseInvoiceId = overSeed.InvoiceId,
                    Amount = 81m,
                    PaymentMethod = "cash",
                    Notes = marker
                });
            });
            AddResult("4. Supplier payment overpayment - fails", "failure", overResult.Success ? "success" : "failure");
            AddResult("4. Supplier payment overpayment - remaining unchanged", 80m, (await GetPurchaseInvoiceAsync(overSeed.InvoiceId)).RemainingAmount);

            var unallocatedSupplierId = await SeedSupplierAsync(marker, "supplier-payment-unallocated", 50m);
            var unallocatedResult = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreateSupplierPaymentService(context).AddPaymentAsync(new SupplierPaymentCreateModel
                {
                    SupplierId = unallocatedSupplierId,
                    Amount = 51m,
                    PaymentMethod = "cash",
                    Notes = marker
                });
            });
            AddResult("4. Supplier unallocated payment above balance - fails", "failure", unallocatedResult.Success ? "success" : "failure");
            AddResult("4. Supplier unallocated payment above balance - balance unchanged", 50m, await GetSupplierBalanceAsync(unallocatedSupplierId));
        }

        private static async Task RunValidPurchaseInvoiceCreateAsync(string marker)
        {
            var supplierId = await SeedSupplierAsync(marker, "purchase-valid-supplier", 100m);
            var materialId = await SeedMaterialAsync(marker, "purchase-valid-material", 10m, 40m);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreatePurchaseInvoiceService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
                {
                    SupplierId = supplierId,
                    DiscountAmount = 10m,
                    PaidAmount = 50m,
                    Items = new List<PurchaseInvoiceItemCreateModel>
                    {
                        new PurchaseInvoiceItemCreateModel { MaterialId = materialId, Quantity = 3m, UnitPrice = 40m }
                    }
                });
            });

            var invoice = await GetLatestPurchaseInvoiceForSupplierAsync(supplierId);
            AddResult("5. Purchase invoice valid create - succeeds", "success", result.Success ? "success" : result.Message);
            AddResult("5. Purchase invoice valid create - net stored total", 110m, invoice.TotalAmount);
            AddResult("5. Purchase invoice valid create - discount", 10m, invoice.DiscountAmount);
            AddResult("5. Purchase invoice valid create - paid", 50m, invoice.PaidAmount);
            AddResult("5. Purchase invoice valid create - remaining", 60m, invoice.RemainingAmount);
            AddResult("5. Purchase invoice valid create - supplier balance", 160m, await GetSupplierBalanceAsync(supplierId));
            AddResult("5. Purchase invoice valid create - stock", 13m, await GetMaterialQuantityAsync(materialId));
        }

        private static async Task RunInvalidPurchaseInvoiceCreatesAsync(string marker)
        {
            await ExpectPurchaseInvoiceFailureAsync(marker, "purchase-neg-quantity", "negative quantity", -1m, 100m, 0m, 0m);
            await ExpectPurchaseInvoiceFailureAsync(marker, "purchase-neg-price", "negative unit price", 1m, -100m, 0m, 0m);
            await ExpectPurchaseInvoiceFailureAsync(marker, "purchase-neg-discount", "negative discount", 1m, 100m, -1m, 0m);
            await ExpectPurchaseInvoiceFailureAsync(marker, "purchase-neg-paid", "negative paid amount", 1m, 100m, 0m, -1m);
            await ExpectPurchaseInvoiceFailureAsync(marker, "purchase-discount-over-gross", "discount > gross total", 1m, 100m, 101m, 0m);
            await ExpectPurchaseInvoiceFailureAsync(marker, "purchase-paid-over-net", "paid > net due", 1m, 100m, 10m, 91m);
        }

        private static async Task ExpectPurchaseInvoiceFailureAsync(
            string marker,
            string suffix,
            string caseName,
            decimal quantity,
            decimal unitPrice,
            decimal discount,
            decimal paid)
        {
            var supplierId = await SeedSupplierAsync(marker, suffix + "-supplier", 0m);
            var materialId = await SeedMaterialAsync(marker, suffix + "-material", 10m, 100m);
            var stockBefore = await GetMaterialQuantityAsync(materialId);
            var balanceBefore = await GetSupplierBalanceAsync(supplierId);
            var countBefore = await CountPurchaseInvoicesForSupplierAsync(supplierId);

            var result = await TryRunAsync(async () =>
            {
                await using var context = CreateContext();
                await CreatePurchaseInvoiceService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
                {
                    SupplierId = supplierId,
                    DiscountAmount = discount,
                    PaidAmount = paid,
                    Items = new List<PurchaseInvoiceItemCreateModel>
                    {
                        new PurchaseInvoiceItemCreateModel { MaterialId = materialId, Quantity = quantity, UnitPrice = unitPrice }
                    }
                });
            });

            AddResult($"5. Purchase invoice {caseName} - fails", "failure", result.Success ? "success" : "failure");
            AddResult($"5. Purchase invoice {caseName} - no invoice persisted", countBefore, await CountPurchaseInvoicesForSupplierAsync(supplierId));
            AddResult($"5. Purchase invoice {caseName} - stock unchanged", stockBefore, await GetMaterialQuantityAsync(materialId));
            AddResult($"5. Purchase invoice {caseName} - balance unchanged", balanceBefore, await GetSupplierBalanceAsync(supplierId));
        }

        private static async Task<SalesInvoiceSeed> CreateSalesInvoiceSeedAsync(
            string marker,
            string suffix,
            decimal initialClientBalance,
            decimal quantity,
            decimal unitPrice,
            decimal discount,
            decimal paid)
        {
            var clientId = await SeedClientAsync(marker, suffix + "-client", initialClientBalance);
            var materialId = await SeedMaterialAsync(marker, suffix + "-material", 100m, unitPrice);

            await using var context = CreateContext();
            await CreateSalesInvoiceService(context).CreateInvoiceAsync(new SalesInvoiceCreateModel
            {
                ClientId = clientId,
                DiscountAmount = discount,
                PaidAmount = paid,
                Items = new List<SalesInvoiceItemCreateModel>
                {
                    new SalesInvoiceItemCreateModel { MaterialId = materialId, Quantity = quantity, UnitPrice = unitPrice }
                }
            });

            var invoice = await GetLatestSalesInvoiceForClientAsync(clientId);
            return new SalesInvoiceSeed(clientId, invoice.Id);
        }

        private static async Task<PurchaseInvoiceSeed> CreatePurchaseInvoiceSeedAsync(
            string marker,
            string suffix,
            decimal initialSupplierBalance,
            decimal quantity,
            decimal unitPrice,
            decimal discount,
            decimal paid)
        {
            var supplierId = await SeedSupplierAsync(marker, suffix + "-supplier", initialSupplierBalance);
            var materialId = await SeedMaterialAsync(marker, suffix + "-material", 10m, unitPrice);

            await using var context = CreateContext();
            await CreatePurchaseInvoiceService(context).CreateInvoiceAsync(new PurchaseInvoiceCreateModel
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
            return new PurchaseInvoiceSeed(supplierId, invoice.Id);
        }

        private static SalesInvoiceService CreateSalesInvoiceService(MaterialManagementContext context)
        {
            return new SalesInvoiceService(
                new SalesInvoiceRepo(context),
                new MaterialRepo(context),
                new ClientRepo(context),
                context,
                Mapper);
        }

        private static PurchaseInvoiceService CreatePurchaseInvoiceService(MaterialManagementContext context)
        {
            return new PurchaseInvoiceService(
                new PurchaseInvoiceRepo(context),
                new MaterialRepo(context),
                new SupplierRepo(context),
                new ClientRepo(context),
                context,
                Mapper);
        }

        private static ClientPaymentService CreateClientPaymentService(MaterialManagementContext context)
        {
            return new ClientPaymentService(
                new ClientPaymentRepo(context),
                new ClientRepo(context),
                new SalesInvoiceRepo(context),
                context,
                Mapper);
        }

        private static SupplierPaymentService CreateSupplierPaymentService(MaterialManagementContext context)
        {
            return new SupplierPaymentService(
                new SupplierPaymentRepo(context),
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

        private static async Task<int> SeedSupplierAsync(string marker, string suffix, decimal balance)
        {
            await using var context = CreateContext();
            var supplier = new Supplier
            {
                Name = $"{marker}-{suffix}",
                Phone = CreatePhone(marker, suffix),
                Balance = balance,
                Address = marker,
                IsActive = true
            };

            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            return supplier.Id;
        }

        private static async Task<int> SeedMaterialAsync(string marker, string suffix, decimal quantity, decimal sellingPrice)
        {
            await using var context = CreateContext();
            var material = new Material
            {
                Name = $"{marker}-{suffix}",
                Code = CreateMaterialCode(marker, suffix),
                Unit = "pcs",
                Quantity = quantity,
                ReservedQuantity = 0m,
                PurchasePrice = sellingPrice,
                SellingPrice = sellingPrice,
                Description = marker,
                IsActive = true
            };

            context.Materials.Add(material);
            await context.SaveChangesAsync();
            return material.Id;
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

        private static async Task<PurchaseInvoice> GetLatestPurchaseInvoiceForSupplierAsync(int supplierId)
        {
            await using var context = CreateContext();
            return await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(invoice => invoice.SupplierId == supplierId)
                .OrderByDescending(invoice => invoice.Id)
                .SingleAsync();
        }

        private static async Task<SalesInvoice> GetSalesInvoiceAsync(int invoiceId)
        {
            await using var context = CreateContext();
            return await context.SalesInvoices
                .IgnoreQueryFilters()
                .SingleAsync(invoice => invoice.Id == invoiceId);
        }

        private static async Task<PurchaseInvoice> GetPurchaseInvoiceAsync(int invoiceId)
        {
            await using var context = CreateContext();
            return await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .SingleAsync(invoice => invoice.Id == invoiceId);
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

        private static async Task<decimal> GetSupplierBalanceAsync(int supplierId)
        {
            await using var context = CreateContext();
            return await context.Suppliers
                .IgnoreQueryFilters()
                .Where(supplier => supplier.Id == supplierId)
                .Select(supplier => supplier.Balance)
                .SingleAsync();
        }

        private static async Task<decimal> GetMaterialQuantityAsync(int materialId)
        {
            await using var context = CreateContext();
            return await context.Materials
                .IgnoreQueryFilters()
                .Where(material => material.Id == materialId)
                .Select(material => material.Quantity)
                .SingleAsync();
        }

        private static async Task<int> CountSalesInvoicesForClientAsync(int clientId)
        {
            await using var context = CreateContext();
            return await context.SalesInvoices
                .IgnoreQueryFilters()
                .CountAsync(invoice => invoice.ClientId == clientId);
        }

        private static async Task<int> CountPurchaseInvoicesForSupplierAsync(int supplierId)
        {
            await using var context = CreateContext();
            return await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .CountAsync(invoice => invoice.SupplierId == supplierId);
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
                .Where(invoice => invoice.ClientId.HasValue && clientIds.Contains(invoice.ClientId.Value))
                .Select(invoice => invoice.Id)
                .ToListAsync();

            var purchaseInvoiceIds = await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(invoice =>
                    (invoice.SupplierId.HasValue && supplierIds.Contains(invoice.SupplierId.Value)) ||
                    (invoice.ClientId.HasValue && clientIds.Contains(invoice.ClientId.Value)))
                .Select(invoice => invoice.Id)
                .ToListAsync();

            var clientPayments = await context.ClientPayments
                .Where(payment => clientIds.Contains(payment.ClientId) ||
                                  (payment.SalesInvoiceId.HasValue && salesInvoiceIds.Contains(payment.SalesInvoiceId.Value)))
                .ToListAsync();
            context.ClientPayments.RemoveRange(clientPayments);

            var supplierPayments = await context.SupplierPayments
                .Where(payment => supplierIds.Contains(payment.SupplierId) ||
                                  (payment.PurchaseInvoiceId.HasValue && purchaseInvoiceIds.Contains(payment.PurchaseInvoiceId.Value)))
                .ToListAsync();
            context.SupplierPayments.RemoveRange(supplierPayments);

            var salesReturns = await context.SalesReturns
                .IgnoreQueryFilters()
                .Where(salesReturn => salesInvoiceIds.Contains(salesReturn.SalesInvoiceId))
                .ToListAsync();
            context.SalesReturns.RemoveRange(salesReturns);

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
            return $"8{hash}".PadRight(15, '0').Substring(0, 15);
        }

        private static string CreateMaterialCode(string marker, string suffix)
        {
            var normalizedSuffix = suffix.Length <= 28 ? suffix : suffix.Substring(0, 28);
            return $"{marker}-{normalizedSuffix}";
        }

        private sealed record TestResult(string CaseName, string Expected, string Actual, bool Passed);

        private sealed record CommandAttempt(bool Success, string Message);

        private sealed record SalesInvoiceSeed(int ClientId, int InvoiceId);

        private sealed record PurchaseInvoiceSeed(int SupplierId, int InvoiceId);

    }
}
