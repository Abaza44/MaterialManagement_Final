using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaterialManagement.DAL.Entities
{
    public class SalesReturnItem
    {
        [Key]
        public int Id { get; set; }

        public int SalesReturnId { get; set; }
        [ForeignKey("SalesReturnId")]
        public virtual SalesReturn SalesReturn { get; set; }

        public int SalesInvoiceItemId { get; set; }
        [ForeignKey("SalesInvoiceItemId")]
        public virtual SalesInvoiceItem SalesInvoiceItem { get; set; }

        public int MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public virtual Material Material { get; set; }

        public decimal ReturnedQuantity { get; set; }
        
        public decimal OriginalUnitPrice { get; set; }

        public decimal NetUnitPrice { get; set; }

        public decimal TotalReturnNetAmount { get; set; }
    }
}
