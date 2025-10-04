using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaterialManagement.DAL.Entities
{
    public class ClientPayment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClientId { get; set; } // ربط بالعميل

        public int? SalesInvoiceId { get; set; } // ربط بالفاتورة (اختياري)

        [Required]
        public DateTime PaymentDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } // نقدي, شيك, تحويل

        [StringLength(200)]
        public string? Notes { get; set; } // رقم الشيك, ملاحظات

        // Navigation Properties
        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; }

        [ForeignKey("SalesInvoiceId")]
        public virtual SalesInvoice? SalesInvoice { get; set; }
    }
}