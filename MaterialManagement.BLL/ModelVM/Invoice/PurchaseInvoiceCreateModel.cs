using System.ComponentModel.DataAnnotations;

namespace MaterialManagement.BLL.ModelVM.Invoice
{
    public class PurchaseInvoiceCreateModel
    {
        
        [StringLength(50)]
        [Display(Name = "رقم الفاتورة")]
        public string? InvoiceNumber { get; set; }

        [Display(Name = "تاريخ الفاتورة")]
        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "المورد مطلوب")]
        [Display(Name = "المورد")]
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "أضف عناصر الفاتورة")]
        public List<PurchaseInvoiceItemCreateModel> Items { get; set; } = new();

        [Display(Name = "المبلغ المدفوع")]
        public decimal PaidAmount { get; set; }

        public string? Notes { get; set; }
    }

    public class PurchaseInvoiceItemCreateModel
    {
        [Required]
        public int MaterialId { get; set; }

        [Required]
        [Range(1, double.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
        public decimal Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "سعر الوحدة يجب أن يكون أكبر من صفر")]
        public decimal UnitPrice { get; set; }
    }
}