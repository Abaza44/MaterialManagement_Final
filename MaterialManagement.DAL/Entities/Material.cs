using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Entities
{
    public class Material
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; }

        [Required]
        [StringLength(20)]
        public string Unit { get; set; }

        public decimal Quantity { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? SellingPrice { get; set; }

        public decimal ReservedQuantity { get; set; } = 0;
        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public virtual ICollection<SalesInvoiceItem> SalesInvoiceItems { get; set; } = new HashSet<SalesInvoiceItem>();
        public virtual ICollection<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; } = new HashSet<PurchaseInvoiceItem>();
    }
}
