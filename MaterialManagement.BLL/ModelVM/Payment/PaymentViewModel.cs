using System;
using System.ComponentModel.DataAnnotations;

namespace MaterialManagement.BLL.ModelVM.Payment
{
    public class ClientPaymentViewModel
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public int? SalesInvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string? Notes { get; set; }
    }

    public class ClientPaymentCreateModel
    {
        [Required]
        [Display(Name = "العميل")]
        public int ClientId { get; set; }

        [Display(Name = "الفاتورة (اختياري)")]
        public int? SalesInvoiceId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ الدفع")]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        [Display(Name = "المبلغ المحصّل")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "طريقة الدفع")]
        public string PaymentMethod { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
    }

    public class SupplierPaymentViewModel
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public int? PurchaseInvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string? Notes { get; set; }
    }

    public class SupplierPaymentCreateModel
    {
        [Required]
        [Display(Name = "المورد")]
        public int SupplierId { get; set; }

        [Display(Name = "فاتورة الشراء (اختياري)")]
        public int? PurchaseInvoiceId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ الدفع")]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        [Display(Name = "المبلغ المدفوع")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "طريقة الدفع")]
        public string PaymentMethod { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
    }
}