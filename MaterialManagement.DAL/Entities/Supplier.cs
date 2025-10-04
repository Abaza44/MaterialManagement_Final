using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Entities
{
    public class Supplier
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(15)]
        public string? Phone { get; set; }

        //[StringLength(100)]
        //public string? Email { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        public decimal Balance { get; set; } = 0;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public virtual ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new HashSet<PurchaseInvoice>();
        // ...
        
        public virtual ICollection<SupplierPayment> Payments { get; set; } = new List<SupplierPayment>(); // <-- أضف هذا
    }
}
