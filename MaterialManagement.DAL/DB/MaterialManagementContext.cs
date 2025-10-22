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
            modelBuilder.Entity<PurchaseInvoice>().Property(i => i.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseInvoice>().Property(i => i.PaidAmount).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseInvoice>().Property(i => i.RemainingAmount).HasPrecision(18, 2);

            // Invoice Items
            modelBuilder.Entity<SalesInvoiceItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<SalesInvoiceItem>().Property(i => i.TotalPrice).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseInvoiceItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseInvoiceItem>().Property(i => i.TotalPrice).HasPrecision(18, 2);

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

            // Supplier to Payments
            modelBuilder.Entity<Supplier>()
                .HasMany(s => s.Payments)
                .WithOne(p => p.Supplier)
                .HasForeignKey(p => p.SupplierId)
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
        }
    }
}