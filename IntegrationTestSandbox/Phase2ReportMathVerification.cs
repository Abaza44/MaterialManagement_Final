using AutoMapper;
using MaterialManagement.BLL.Helper;
using MaterialManagement.BLL.Service.Implementations;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTestSandbox
{
    internal static class Phase2ReportMathVerification
    {
        private const string ConnectionString = "Server=.;Database=MaterialManagementDB;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";

        private static readonly List<TestResult> Results = new();
        private static readonly IMapper Mapper = CreateMapper();

        public static async Task<int> RunAsync()
        {
            var marker = $"P2RM{DateTime.UtcNow:HHmmssfff}";
            Results.Clear();
            Console.WriteLine($"Starting Phase 2 Part D report math verification. Marker={marker}");

            try
            {
                await CleanupAsync(marker);

                await RunClientStatementDiscountAndPaymentAsync(marker);
                await RunSupplierStatementDiscountAndPaymentAsync(marker);
                await RunClientReturnPurchaseStatementAsync(marker);
                await RunSalesReturnStatementAndMaterialMovementAsync(marker);
                await RunProfitReportNetSalesAndReturnsAsync(marker);
                await RunInitialPaidClampAsync(marker);
            }
            finally
            {
                await CleanupAsync(marker);
            }

            Console.WriteLine();
            Console.WriteLine("=== Phase 2 Part D Report Math Verification Results ===");
            foreach (var result in Results)
            {
                Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} | {result.CaseName} | Expected: {result.Expected} | Actual: {result.Actual}");
            }

            var failedCount = Results.Count(result => !result.Passed);
            Console.WriteLine($"SUMMARY: {Results.Count - failedCount}/{Results.Count} checks passed.");
            return failedCount == 0 ? 0 : 1;
        }

        private static async Task RunClientStatementDiscountAndPaymentAsync(string marker)
        {
            await using var seedContext = CreateContext();
            var client = CreateClient(marker, "client-statement", 40m);
            var material = CreateMaterial(marker, "client-statement-material", 100m, 30m);
            var invoice = CreateSalesInvoice(marker, "client-statement-sale", client, material, 2m, 50m, 10m, 50m);
            seedContext.SalesInvoices.Add(invoice);
            seedContext.ClientPayments.Add(new ClientPayment
            {
                Client = client,
                SalesInvoice = invoice,
                PaymentDate = DateTime.Now.AddMinutes(1),
                Amount = 30m,
                PaymentMethod = "cash",
                Notes = marker
            });
            await seedContext.SaveChangesAsync();

            await using var context = CreateContext();
            var statement = await CreateReportService(context).GetClientAccountStatementAsync(
                client.Id,
                DateTime.Today.AddDays(-1),
                DateTime.Today.AddDays(1));

            var invoiceRow = statement.Single(row => row.Reference == invoice.InvoiceNumber);
            var paymentRow = statement.Single(row => row.Reference.StartsWith("دفعة #"));
            var finalBalance = statement.Last().Balance;

            AddResult("1. Client statement - sales invoice uses net debit", 90m, invoiceRow.Debit);
            AddResult("1. Client statement - initial paid reconstructed only", 20m, invoiceRow.Credit);
            AddResult("1. Client statement - linked payment remains separate", 30m, paymentRow.Credit);
            AddResult("1. Client statement - no double count final balance", 40m, finalBalance);
        }

        private static async Task RunSupplierStatementDiscountAndPaymentAsync(string marker)
        {
            await using var seedContext = CreateContext();
            var supplier = CreateSupplier(marker, "supplier-statement", 40m);
            var material = CreateMaterial(marker, "supplier-statement-material", 100m, 30m);
            var invoice = CreatePurchaseInvoice(marker, "supplier-statement-purchase", supplier, null, material, 2m, 50m, 10m, 50m);
            seedContext.PurchaseInvoices.Add(invoice);
            seedContext.SupplierPayments.Add(new SupplierPayment
            {
                Supplier = supplier,
                PurchaseInvoice = invoice,
                PaymentDate = DateTime.Now.AddMinutes(1),
                Amount = 30m,
                PaymentMethod = "cash",
                Notes = marker
            });
            await seedContext.SaveChangesAsync();

            await using var context = CreateContext();
            var statement = await CreateReportService(context).GetSupplierAccountStatementAsync(
                supplier.Id,
                DateTime.Today.AddDays(-1),
                DateTime.Today.AddDays(1));

            var invoiceRow = statement.Single(row => row.Reference == invoice.InvoiceNumber);
            var paymentRow = statement.Single(row => row.Reference.StartsWith("دفعة #"));
            var finalBalance = statement.Last().Balance;

            AddResult("2. Supplier statement - purchase invoice uses stored net total", 90m, invoiceRow.Debit);
            AddResult("2. Supplier statement - does not subtract discount again", 90m, invoiceRow.Debit);
            AddResult("2. Supplier statement - initial paid reconstructed only", 20m, invoiceRow.Credit);
            AddResult("2. Supplier statement - linked payment remains separate", 30m, paymentRow.Credit);
            AddResult("2. Supplier statement - no double count final balance", 40m, finalBalance);
        }

        private static async Task RunClientReturnPurchaseStatementAsync(string marker)
        {
            await using var seedContext = CreateContext();
            var client = CreateClient(marker, "client-return", -40m);
            var material = CreateMaterial(marker, "client-return-material", 100m, 20m);
            var invoice = CreatePurchaseInvoice(marker, "client-return-purchase", null, client, material, 2m, 50m, 0m, 60m);
            seedContext.PurchaseInvoices.Add(invoice);
            await seedContext.SaveChangesAsync();

            await using var context = CreateContext();
            var statement = await CreateReportService(context).GetClientAccountStatementAsync(
                client.Id,
                DateTime.Today.AddDays(-1),
                DateTime.Today.AddDays(1));

            var returnRow = statement.Single(row => row.Reference == invoice.InvoiceNumber);
            AddResult("3. Client-return purchase invoice - credit uses remaining", 40m, returnRow.Credit);
            AddResult("3. Client-return purchase invoice - final balance matches write flow", -40m, statement.Last().Balance);
        }

        private static async Task RunSalesReturnStatementAndMaterialMovementAsync(string marker)
        {
            await using var seedContext = CreateContext();
            var client = CreateClient(marker, "sales-return", 70m);
            var material = CreateMaterial(marker, "sales-return-material", 100m, 30m);
            var invoice = CreateSalesInvoice(marker, "sales-return-original", client, material, 2m, 50m, 0m, 0m);
            invoice.InvoiceDate = DateTime.Today.AddDays(-3);
            var salesReturn = CreateSalesReturn(marker, "sales-return-posted", invoice, client, material, 1m, 50m, 30m);
            seedContext.SalesInvoices.Add(invoice);
            seedContext.SalesReturns.Add(salesReturn);
            await seedContext.SaveChangesAsync();

            await using var context = CreateContext();
            var service = CreateReportService(context);
            var statement = await service.GetClientAccountStatementAsync(client.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));
            var movement = await service.GetMaterialMovementAsync(material.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

            var returnStatementRow = statement.Single(row => row.Reference == salesReturn.ReturnNumber);
            var returnMovementRow = movement.Single(row => row.InvoiceNumber == salesReturn.ReturnNumber);
            AddResult("4. SalesReturn statement - posted active return credited", 30m, returnStatementRow.Credit);
            AddResult("4. SalesReturn statement - final balance reflects return", 70m, statement.Last().Balance);
            AddResult("4. Material movement - SalesReturn included as inbound", 1m, returnMovementRow.QuantityIn);
            AddResult("4. Material movement - SalesReturn type", "مرتجع بيع", returnMovementRow.TransactionType);
        }

        private static async Task RunProfitReportNetSalesAndReturnsAsync(string marker)
        {
            await using var seedContext = CreateContext();
            var client = CreateClient(marker, "profit", 45m);
            var material = CreateMaterial(marker, "profit-material", 100m, 30m);
            var invoice = CreateSalesInvoice(marker, "profit-sale", client, material, 2m, 50m, 10m, 0m);
            var salesReturn = CreateSalesReturn(marker, "profit-return", invoice, client, material, 1m, 50m, 45m);
            seedContext.SalesInvoices.Add(invoice);
            seedContext.SalesReturns.Add(salesReturn);
            await seedContext.SaveChangesAsync();

            await using var context = CreateContext();
            var profit = await CreateReportService(context).GetProfitReportAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

            var saleRow = profit.Single(row => row.InvoiceNumber == invoice.InvoiceNumber);
            var returnRow = profit.Single(row => row.InvoiceNumber == salesReturn.ReturnNumber);
            AddResult("5. Profit report - sale revenue uses net", 90m, saleRow.TotalAmount);
            AddResult("5. Profit report - sale profit uses net revenue", 30m, saleRow.Profit);
            AddResult("5. Profit report - SalesReturn negative revenue", -45m, returnRow.TotalAmount);
            AddResult("5. Profit report - SalesReturn reverses approximate cost", -30m, returnRow.TotalCost);
            AddResult("5. Profit report - total profit reduced by return", 15m, saleRow.Profit + returnRow.Profit);
        }

        private static async Task RunInitialPaidClampAsync(string marker)
        {
            await using var seedContext = CreateContext();
            var client = CreateClient(marker, "clamp", -10m);
            var material = CreateMaterial(marker, "clamp-material", 100m, 30m);
            var invoice = CreateSalesInvoice(marker, "clamp-sale", client, material, 1m, 50m, 0m, 10m);
            seedContext.SalesInvoices.Add(invoice);
            seedContext.ClientPayments.Add(new ClientPayment
            {
                Client = client,
                SalesInvoice = invoice,
                PaymentDate = DateTime.Now.AddMinutes(1),
                Amount = 20m,
                PaymentMethod = "cash",
                Notes = marker
            });
            await seedContext.SaveChangesAsync();

            await using var context = CreateContext();
            var statement = await CreateReportService(context).GetClientAccountStatementAsync(
                client.Id,
                DateTime.Today.AddDays(-1),
                DateTime.Today.AddDays(1));

            var invoiceRow = statement.Single(row => row.Reference == invoice.InvoiceNumber);
            AddResult("6. Initial paid reconstruction - negative clamp", 0m, invoiceRow.Credit);
        }

        private static SalesInvoice CreateSalesInvoice(
            string marker,
            string suffix,
            Client client,
            Material material,
            decimal quantity,
            decimal unitPrice,
            decimal discount,
            decimal cumulativePaid)
        {
            var gross = quantity * unitPrice;
            return new SalesInvoice
            {
                Client = client,
                InvoiceNumber = $"SAL-{marker}-{suffix}",
                InvoiceDate = DateTime.Now,
                TotalAmount = gross,
                DiscountAmount = discount,
                PaidAmount = cumulativePaid,
                RemainingAmount = gross - discount - cumulativePaid,
                Notes = marker,
                IsActive = true,
                SalesInvoiceItems = new List<SalesInvoiceItem>
                {
                    new SalesInvoiceItem
                    {
                        Material = material,
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        TotalPrice = gross
                    }
                }
            };
        }

        private static PurchaseInvoice CreatePurchaseInvoice(
            string marker,
            string suffix,
            Supplier? supplier,
            Client? client,
            Material material,
            decimal quantity,
            decimal unitPrice,
            decimal discount,
            decimal cumulativePaid)
        {
            var gross = quantity * unitPrice;
            var net = gross - discount;
            var invoice = new PurchaseInvoice
            {
                InvoiceNumber = $"PUR-{marker}-{suffix}",
                InvoiceDate = DateTime.Now,
                TotalAmount = net,
                DiscountAmount = discount,
                PaidAmount = cumulativePaid,
                RemainingAmount = net - cumulativePaid,
                Notes = marker,
                IsActive = true,
                PurchaseInvoiceItems = new List<PurchaseInvoiceItem>
                {
                    new PurchaseInvoiceItem
                    {
                        Material = material,
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        TotalPrice = gross
                    }
                }
            };

            if (supplier != null)
            {
                invoice.Supplier = supplier;
            }

            if (client != null)
            {
                invoice.Client = client;
            }

            return invoice;
        }

        private static SalesReturn CreateSalesReturn(
            string marker,
            string suffix,
            SalesInvoice invoice,
            Client client,
            Material material,
            decimal returnedQuantity,
            decimal originalUnitPrice,
            decimal totalNetAmount)
        {
            return new SalesReturn
            {
                ReturnNumber = $"SR-{marker}-{suffix}",
                SalesInvoice = invoice,
                Client = client,
                ReturnDate = DateTime.Now,
                Status = ReturnStatus.Posted,
                TotalGrossAmount = returnedQuantity * originalUnitPrice,
                TotalProratedDiscount = returnedQuantity * originalUnitPrice - totalNetAmount,
                TotalNetAmount = totalNetAmount,
                Notes = marker,
                IsActive = true,
                SalesReturnItems = new List<SalesReturnItem>
                {
                    new SalesReturnItem
                    {
                        SalesInvoiceItem = invoice.SalesInvoiceItems.Single(),
                        Material = material,
                        ReturnedQuantity = returnedQuantity,
                        OriginalUnitPrice = originalUnitPrice,
                        NetUnitPrice = totalNetAmount / returnedQuantity,
                        TotalReturnNetAmount = totalNetAmount
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

        private static Material CreateMaterial(string marker, string suffix, decimal quantity, decimal purchasePrice)
        {
            return new Material
            {
                Name = $"{marker}-{suffix}",
                Code = CreateMaterialCode(marker, suffix),
                Unit = "pcs",
                Quantity = quantity,
                ReservedQuantity = 0m,
                PurchasePrice = purchasePrice,
                SellingPrice = purchasePrice,
                Description = marker,
                IsActive = true
            };
        }

        private static ReportService CreateReportService(MaterialManagementContext context)
        {
            return new ReportService(context, Mapper);
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

            var salesReturnIds = await context.SalesReturns
                .IgnoreQueryFilters()
                .Where(salesReturn => salesReturn.Notes == marker || salesReturn.ReturnNumber.Contains(marker) || clientIds.Contains(salesReturn.ClientId))
                .Select(salesReturn => salesReturn.Id)
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

            var salesReturnItems = await context.SalesReturnItems
                .IgnoreQueryFilters()
                .Where(item =>
                    salesReturnIds.Contains(item.SalesReturnId) ||
                    salesInvoiceIds.Contains(item.SalesInvoiceItem.SalesInvoiceId))
                .ToListAsync();
            context.SalesReturnItems.RemoveRange(salesReturnItems);

            var salesReturns = await context.SalesReturns
                .IgnoreQueryFilters()
                .Where(salesReturn => salesReturnIds.Contains(salesReturn.Id))
                .ToListAsync();
            context.SalesReturns.RemoveRange(salesReturns);

            await context.SaveChangesAsync();

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
            return $"5{hash}".PadRight(15, '0').Substring(0, 15);
        }

        private static string CreateMaterialCode(string marker, string suffix)
        {
            var maxSuffixLength = Math.Max(1, 49 - marker.Length);
            var normalizedSuffix = suffix.Length <= maxSuffixLength ? suffix : suffix.Substring(0, maxSuffixLength);
            return $"{marker}-{normalizedSuffix}";
        }

        private sealed record TestResult(string CaseName, string Expected, string Actual, bool Passed);
    }
}
