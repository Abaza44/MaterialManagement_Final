using MaterialManagement.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.DB
{
    public class MaterialManagementContext : DbContext
    {

        public MaterialManagementContext(DbContextOptions<MaterialManagementContext> options) : base(options)
        {
        }

        public DbSet<Material> Materials { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SalesInvoice> SalesInvoices { get; set; }
        public DbSet<SalesInvoiceItem> SalesInvoiceItems { get; set; }
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
        public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Equipment> Equipment { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
        public DbSet<ClientPayment> ClientPayments { get; set; }
        public DbSet<SupplierPayment> SupplierPayments { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<ReservationItem> ReservationItems { get; set; }
        public DbSet<SalesReturn> SalesReturns { get; set; }
        public DbSet<SalesReturnItem> SalesReturnItems { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- Precision for decimal properties ---

            // Material
            modelBuilder.Entity<Material>().Property(m => m.Quantity).HasPrecision(18, 2);
            modelBuilder.Entity<Material>().Property(m => m.PurchasePrice).HasPrecision(18, 2);
            modelBuilder.Entity<Material>().Property(m => m.SellingPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Material>().Property(m => m.ReservedQuantity).HasPrecision(18, 2);

            // Client & Supplier
            modelBuilder.Entity<Client>().Property(c => c.Balance).HasPrecision(18, 2);
            modelBuilder.Entity<Supplier>().Property(s => s.Balance).HasPrecision(18, 2);

            // Invoices
            modelBuilder.Entity<SalesInvoice>().Property(i => i.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<SalesInvoice>().Property(i => i.PaidAmount).HasPrecision(18, 2);
            modelBuilder.Entity<SalesInvoice>().Property(i => i.RemainingAmount).HasPrecision(18, 2);
            modelBuilder.Entity<SalesInvoice>().Property(i => i.OneTimeCustomerName).HasMaxLength(100);
            modelBuilder.Entity<SalesInvoice>().Property(i => i.OneTimeCustomerPhone).HasMaxLength(30);
            modelBuilder.Entity<PurchaseInvoice>().Property(i => i.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseInvoice>().Property(i => i.PaidAmount).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseInvoice>().Property(i => i.RemainingAmount).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseInvoice>().Property(i => i.OneTimeSupplierName).HasMaxLength(100);
            modelBuilder.Entity<PurchaseInvoice>().Property(i => i.OneTimeSupplierPhone).HasMaxLength(30);

            // Invoice Items
            modelBuilder.Entity<SalesInvoiceItem>().Property(i => i.Quantity).HasPrecision(18, 2);
            modelBuilder.Entity<SalesInvoiceItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<SalesInvoiceItem>().Property(i => i.TotalPrice).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseInvoiceItem>().Property(i => i.Quantity).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseInvoiceItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseInvoiceItem>().Property(i => i.TotalPrice).HasPrecision(18, 2);

            // SalesReturn / SalesReturnItem
            modelBuilder.Entity<SalesReturn>().Property(r => r.TotalGrossAmount).HasPrecision(18, 2);
            modelBuilder.Entity<SalesReturn>().Property(r => r.TotalProratedDiscount).HasPrecision(18, 2);
            modelBuilder.Entity<SalesReturn>().Property(r => r.TotalNetAmount).HasPrecision(18, 2);
            modelBuilder.Entity<SalesReturnItem>().Property(ri => ri.ReturnedQuantity).HasPrecision(18, 2);
            modelBuilder.Entity<SalesReturnItem>().Property(ri => ri.OriginalUnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<SalesReturnItem>().Property(ri => ri.NetUnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<SalesReturnItem>().Property(ri => ri.TotalReturnNetAmount).HasPrecision(18, 2);

            // Payments
            modelBuilder.Entity<ClientPayment>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<SupplierPayment>().Property(p => p.Amount).HasPrecision(18, 2);

            // Maintenance
            modelBuilder.Entity<MaintenanceRecord>().Property(r => r.Cost).HasPrecision(18, 2);

            // Expense
            modelBuilder.Entity<Expense>().Property(e => e.Amount).HasPrecision(18, 2);
            //===============================Reservation=====================
            modelBuilder.Entity<Reservation>().Property(r => r.TotalAmount).HasPrecision(18, 2);
            // Add precision for ReservationItem
            modelBuilder.Entity<ReservationItem>().Property(ri => ri.Quantity).HasPrecision(18, 2);
            modelBuilder.Entity<ReservationItem>().Property(ri => ri.FulfilledQuantity).HasPrecision(18, 2);
            modelBuilder.Entity<ReservationItem>().Property(ri => ri.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<ReservationItem>().Property(ri => ri.TotalPrice).HasPrecision(18, 2);
            // --- Relationships ---

            // Equipment to Maintenance Records
            modelBuilder.Entity<Equipment>()
                .HasMany(e => e.MaintenanceHistory)
                .WithOne(m => m.Equipment)
                .HasForeignKey(m => m.EquipmentCode)
                .OnDelete(DeleteBehavior.Cascade); // إذا حذفت معدة، احذف سجلات صيانتها

            // Client to Payments
            modelBuilder.Entity<Client>()
                .HasMany(c => c.Payments)
                .WithOne(p => p.Client)
                .HasForeignKey(p => p.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Client to SalesInvoices is optional because walk-in customers are stored on the invoice.
            modelBuilder.Entity<Client>()
                .HasMany(c => c.SalesInvoices)
                .WithOne(i => i.Client)
                .HasForeignKey(i => i.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Supplier to Payments
            modelBuilder.Entity<Supplier>()
                .HasMany(s => s.Payments)
                .WithOne(p => p.Supplier)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            // Supplier to PurchaseInvoices is optional because one-time suppliers are stored on the invoice.
            modelBuilder.Entity<Supplier>()
                .HasMany(s => s.PurchaseInvoices)
                .WithOne(i => i.Supplier)
                .HasForeignKey(i => i.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            // SalesInvoice to Items
            modelBuilder.Entity<SalesInvoice>()
                .HasMany(i => i.SalesInvoiceItems)
                .WithOne(item => item.SalesInvoice)
                .HasForeignKey(item => item.SalesInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            // PurchaseInvoice to Items
            modelBuilder.Entity<PurchaseInvoice>()
                .HasMany(i => i.PurchaseInvoiceItems)
                .WithOne(item => item.PurchaseInvoice)
                .HasForeignKey(item => item.PurchaseInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Reservation to its Items relationship
            modelBuilder.Entity<Reservation>()
                .HasMany(r => r.ReservationItems)
                .WithOne(item => item.Reservation)
                .HasForeignKey(item => item.ReservationId)
                .OnDelete(DeleteBehavior.Cascade); // If a reservation is deleted, delete its items
            
            // --- Unique Indexes ---
            modelBuilder.Entity<Material>().HasIndex(m => m.Code).IsUnique();
            modelBuilder.Entity<Client>().HasIndex(c => c.Phone).IsUnique();
            modelBuilder.Entity<Supplier>().HasIndex(s => s.Phone).IsUnique();
            modelBuilder.Entity<SalesInvoice>().HasIndex(s => s.InvoiceNumber).IsUnique();
            modelBuilder.Entity<PurchaseInvoice>().HasIndex(p => p.InvoiceNumber).IsUnique();
            modelBuilder.Entity<Reservation>().HasIndex(r => r.ReservationNumber).IsUnique();
            modelBuilder.Entity<SalesReturn>().HasIndex(r => r.ReturnNumber).IsUnique();

            // --- SalesReturn Relationships ---

            // SalesInvoice has many SalesReturns. RESTRICT delete to block invoice deletion if returns exist.
            modelBuilder.Entity<SalesInvoice>()
                .HasMany(i => i.SalesReturns)
                .WithOne(r => r.SalesInvoice)
                .HasForeignKey(r => r.SalesInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Client has many SalesReturns
            modelBuilder.Entity<Client>()
                .HasMany<SalesReturn>()
                .WithOne(r => r.Client)
                .HasForeignKey(r => r.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // SalesReturn has many SalesReturnItems
            modelBuilder.Entity<SalesReturn>()
                .HasMany(r => r.SalesReturnItems)
                .WithOne(ri => ri.SalesReturn)
                .HasForeignKey(ri => ri.SalesReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            // SalesInvoiceItem has many SalesReturnItems. RESTRICT to block modifying original items with active returns.
            modelBuilder.Entity<SalesInvoiceItem>()
                .HasMany(i => i.SalesReturnItems)
                .WithOne(ri => ri.SalesInvoiceItem)
                .HasForeignKey(ri => ri.SalesInvoiceItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // Material to SalesReturnItem (no cascade - material lives independently)
            modelBuilder.Entity<Material>()
                .HasMany<SalesReturnItem>()
                .WithOne(ri => ri.Material)
                .HasForeignKey(ri => ri.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Global Soft Delete Query Filters ---
            modelBuilder.Entity<Client>().HasQueryFilter(e => e.IsActive);
            modelBuilder.Entity<Supplier>().HasQueryFilter(e => e.IsActive);
            modelBuilder.Entity<Material>().HasQueryFilter(e => e.IsActive);
            modelBuilder.Entity<SalesInvoice>().HasQueryFilter(e => e.IsActive);
            modelBuilder.Entity<PurchaseInvoice>().HasQueryFilter(e => e.IsActive);
            
            // Do not filter historical child rows by related active state.
            // Invoice, payment, reservation, and return history must remain readable
            // even if a related material/client/supplier is later deactivated.
            modelBuilder.Entity<SalesReturn>().HasQueryFilter(e => e.IsActive);
        }
    }
}
