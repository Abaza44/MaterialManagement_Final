using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Entities
{
    public class SalesInvoice
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; }

        public DateTime InvoiceDate { get; set; }

        public int ClientId { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0; // الخصم
        public decimal RemainingAmount { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public virtual Client Client { get; set; }
        public virtual ICollection<SalesInvoiceItem> SalesInvoiceItems { get; set; } = new HashSet<SalesInvoiceItem>();
    }
}
