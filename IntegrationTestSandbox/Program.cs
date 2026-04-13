using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using MaterialManagement.BLL.Features.Invoicing.Commands;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;

namespace IntegrationTestSandbox
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Runtime Parity Tests on SalesInvoice...");
            string connectionString = "Server=.;Database=MaterialManagementDB;Trusted_Connection=True;TrustServerCertificate=True;";
            
            var options = new DbContextOptionsBuilder<MaterialManagementContext>()
                .UseSqlServer(connectionString)
                .Options;

            using var context = new MaterialManagementContext(options);
            
            // Setup mapper
            var config = new MapperConfiguration(cfg => {
                cfg.CreateMap<SalesInvoiceItemCreateModel, SalesInvoiceItem>();
                cfg.CreateMap<SalesInvoice, SalesInvoiceViewModel>();
            });
            var mapper = config.CreateMapper();

            var handler = new CreateSalesInvoiceHandler(context, mapper);

            // Clean database state setup inside test
            var testMaterial = new Material { Name = "Test Material", Code = $"TM{DateTime.Now.Ticks}", Quantity = 100, ReservedQuantity = 0, Unit = "Kg", IsActive = true, Notes = "Test Sandbox" };
            var testClient = new Client { Name = "Test Client", Phone = $"010{DateTime.Now.Ticks.ToString().Substring(0, 8)}", Balance = 1000, IsActive = true };
            context.Materials.Add(testMaterial);
            context.Clients.Add(testClient);
            await context.SaveChangesAsync();
            
            try 
            {
                // TEST 1: Normal Invoice Creation
                Console.WriteLine("\n[Test 1: Normal Invoice (No Discount)]");
                var command1 = new CreateSalesInvoiceCommand {
                    Model = new SalesInvoiceCreateModel {
                        ClientId = testClient.Id,
                        PaidAmount = 200,
                        DiscountAmount = 0,
                        Items = new List<SalesInvoiceItemCreateModel> {
                            new SalesInvoiceItemCreateModel { MaterialId = testMaterial.Id, Quantity = 10, UnitPrice = 50 }
                        }
                    }
                };
                var result1 = await handler.Handle(command1, CancellationToken.None);
                
                await context.Entry(testMaterial).ReloadAsync();
                await context.Entry(testClient).ReloadAsync();
                var savedInvoice = await context.SalesInvoices.Include(i => i.SalesInvoiceItems).FirstOrDefaultAsync(i => i.Id == result1.Id);

                Console.WriteLine($"- InvoiceNumber Match pattern ^SAL-: {savedInvoice.InvoiceNumber.StartsWith("SAL-")}");
                Console.WriteLine($"- Expected TotalAmount 500, Actual: {savedInvoice.TotalAmount}");
                Console.WriteLine($"- Expected RemainingAmount 300, Actual: {savedInvoice.RemainingAmount}");
                Console.WriteLine($"- Expected Material Stock 90 (100 - 10), Actual: {testMaterial.Quantity}");
                Console.WriteLine($"- Expected Client Balance 1300 (1000 + 300), Actual: {testClient.Balance}");
                Console.WriteLine($"- Line Items Persisted: {savedInvoice.SalesInvoiceItems.Count}");


                // TEST 2: Discount Scenario
                Console.WriteLine("\n[Test 2: Discount Scenario]");
                var command2 = new CreateSalesInvoiceCommand {
                    Model = new SalesInvoiceCreateModel {
                        ClientId = testClient.Id,
                        PaidAmount = 100,
                        DiscountAmount = 50,
                        Items = new List<SalesInvoiceItemCreateModel> {
                            new SalesInvoiceItemCreateModel { MaterialId = testMaterial.Id, Quantity = 5, UnitPrice = 100 }
                        }
                    }
                };
                var result2 = await handler.Handle(command2, CancellationToken.None);
                await context.Entry(testClient).ReloadAsync();
                var savedInvoice2 = await context.SalesInvoices.FirstOrDefaultAsync(i => i.Id == result2.Id);
                
                Console.WriteLine($"- Expected TotalAmount 500, Actual: {savedInvoice2.TotalAmount}");
                Console.WriteLine($"- Expected RemainingAmount 350 (500 - 50 discount - 100 paid), Actual: {savedInvoice2.RemainingAmount}");
                Console.WriteLine($"- Expected Client Balance 1650 (1300 + 350), Actual: {testClient.Balance}");


                // TEST 4: Insufficient Stock
                Console.WriteLine("\n[Test 4: Insufficient Stock Rollback Proof]");
                var command4 = new CreateSalesInvoiceCommand {
                    Model = new SalesInvoiceCreateModel {
                        ClientId = testClient.Id,
                        PaidAmount = 0,
                        Items = new List<SalesInvoiceItemCreateModel> {
                            new SalesInvoiceItemCreateModel { MaterialId = testMaterial.Id, Quantity = 5000, UnitPrice = 10 }
                        }
                    }
                };
                try {
                    await handler.Handle(command4, CancellationToken.None);
                    Console.WriteLine("- FAILED: Exception was not thrown!");
                } catch (InvalidOperationException ex) {
                    Console.WriteLine($"- EXPECTED EXCEPTION CAUGHT: {ex.Message}");
                    await context.Entry(testClient).ReloadAsync();
                    await context.Entry(testMaterial).ReloadAsync();
                    Console.WriteLine($"- Expected Stock Unchanged (85): {testMaterial.Quantity}");
                    Console.WriteLine($"- Expected Client Balance Unchanged (1650): {testClient.Balance}");
                }

                
                // TEST 5: Concurrency Collision
                Console.WriteLine("\n[Test 5: Concurrency Collision]");
                var command5 = new CreateSalesInvoiceCommand {
                    Model = new SalesInvoiceCreateModel {
                        ClientId = testClient.Id,
                        PaidAmount = 0,
                        Items = new List<SalesInvoiceItemCreateModel> {
                            new SalesInvoiceItemCreateModel { MaterialId = testMaterial.Id, Quantity = 1, UnitPrice = 10 }
                        }
                    }
                };

                // Create a silent out-of-band edit to trigger raw SQL Concurrency mismatch
                await context.Database.ExecuteSqlRawAsync($"UPDATE Clients SET Balance = Balance + 1 WHERE Id = {testClient.Id}");
                
                try {
                    await handler.Handle(command5, CancellationToken.None);
                    Console.WriteLine("- FAILED: DbUpdateConcurrencyException was not thrown!");
                } catch (DbUpdateConcurrencyException) {
                    Console.WriteLine("- EXPECTED EXCEPTION CAUGHT: DbUpdateConcurrencyException");
                }
            } 
            finally 
            {
                // Cleanup generated test data permanently
                context.Clients.Remove(testClient);
                context.Materials.Remove(testMaterial);
                await context.SaveChangesAsync();
                Console.WriteLine("\nSandbox Execution Complete. All test data cleaned up.");
            }
        }
    }
}
