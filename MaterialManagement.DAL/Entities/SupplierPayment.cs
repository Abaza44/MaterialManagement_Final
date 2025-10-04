using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaterialManagement.DAL.Entities
{
    public class SupplierPayment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; } // ربط بالمورد

        public int? PurchaseInvoiceId { get; set; } // ربط بالفاتورة (اختياري)

        [Required]
        public DateTime PaymentDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; }

        [StringLength(200)]
        public string? Notes { get; set; }

        // Navigation Properties
        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; }

        [ForeignKey("PurchaseInvoiceId")]
        public virtual PurchaseInvoice? PurchaseInvoice { get; set; }
    }
}