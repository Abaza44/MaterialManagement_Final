using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Http;
namespace MaterialManagement.BLL.ModelVM.Invoice
{
    public class PurchaseInvoiceCreateModel
    {
        [StringLength(50)]
        [Display(Name = "رقم الفاتورة (اتركه فارغًا للتوليد التلقائي)")]
        public string? InvoiceNumber { get; set; }

        [Display(Name = "تاريخ العملية")]
        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        // <<< تم التعديل هنا >>>
        // لم يعد المورد مطلوبًا بشكل إلزامي
        [Display(Name = "المورد")]
        public int? SupplierId { get; set; }

        // <<< تم إضافة هذا >>>
        [Display(Name = "العميل (في حالة المرتجع)")]
        public int? ClientId { get; set; }

        [Required(ErrorMessage = "أضف عناصر الفاتورة")]
        public List<PurchaseInvoiceItemCreateModel> Items { get; set; } = new();

        [Display(Name = "المبلغ المسدد/المسترجع")]
        public decimal PaidAmount { get; set; } // هذا المبلغ سيتم خصمه من رصيد المورد أو إضافته لرصيد العميل

        public string? Notes { get; set; }
    }

    public class PurchaseInvoiceItemCreateModel
    {
        [Required]
        public int MaterialId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
        public decimal Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "سعر الوحدة يجب أن يكون أكبر من صفر")]
        public decimal UnitPrice { get; set; }
        public IFormFile? AttachmentFile { get; set; }
    }
}