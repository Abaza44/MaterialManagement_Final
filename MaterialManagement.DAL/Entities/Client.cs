using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Entities
{
    public class Client
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

        
        public virtual ICollection<SalesInvoice> SalesInvoices { get; set; } = new HashSet<SalesInvoice>();
        // ...
        public virtual ICollection<ClientPayment> Payments { get; set; } = new List<ClientPayment>(); // <-- أضف هذا
    }

}
