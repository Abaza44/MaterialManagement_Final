using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MaterialManagement.BLL.ModelVM.Invoice
{
    public class SalesInvoiceCreateModel
    {
        
        [StringLength(50)]
        public string? InvoiceNumber { get; set; }

        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [Required]
        public int ClientId { get; set; }

        [Required]
        public List<SalesInvoiceItemCreateModel> Items { get; set; } = new();

        public decimal PaidAmount { get; set; }
        public string? Notes { get; set; }
    }

    public class SalesInvoiceItemCreateModel
    {
        [Required]
        public int MaterialId { get; set; }

        [Required]
        public decimal Quantity { get; set; }

        [Required]
        public decimal UnitPrice { get; set; }
        public IFormFile? AttachmentFile { get; set; }
    }
}