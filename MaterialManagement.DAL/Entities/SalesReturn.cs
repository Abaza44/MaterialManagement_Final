using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaterialManagement.DAL.Entities
{
    public enum ReturnStatus
    {
        Draft = 0,
        Posted = 1,
        Cancelled = 2
    }

    public class SalesReturn
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReturnNumber { get; set; }

        public int SalesInvoiceId { get; set; }
        [ForeignKey("SalesInvoiceId")]
        public virtual SalesInvoice SalesInvoice { get; set; }

        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; }

        public DateTime ReturnDate { get; set; }
        
        public ReturnStatus Status { get; set; }

        public decimal TotalGrossAmount { get; set; }

        public decimal TotalProratedDiscount { get; set; }

        public decimal TotalNetAmount { get; set; }

        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<SalesReturnItem> SalesReturnItems { get; set; } = new List<SalesReturnItem>();
    }
}
