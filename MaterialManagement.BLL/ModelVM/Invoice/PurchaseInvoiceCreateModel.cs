using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System;

using MaterialManagement.DAL.Enums;

namespace MaterialManagement.BLL.ModelVM.Invoice
{
    public class PurchaseInvoiceCreateModel : IValidatableObject
    {
        [StringLength(50)]
        [Display(Name = "رقم الفاتورة (اتركه فارغًا للتوليد التلقائي)")]
        public string? InvoiceNumber { get; set; }

        [Display(Name = "تاريخ العملية")]
        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [Display(Name = "نوع العملية")]
        public PurchaseInvoicePartyMode PartyMode { get; set; } = PurchaseInvoicePartyMode.RegisteredSupplier;

        // <<< تم التعديل هنا >>>
        // لم يعد المورد مطلوبًا بشكل إلزامي
        [Display(Name = "المورد")]
        [Range(1, int.MaxValue, ErrorMessage = "المورد المحدد غير صالح")]
        public int? SupplierId { get; set; }

        [Display(Name = "اسم المورد اليدوي")]
        [StringLength(100, ErrorMessage = "اسم المورد اليدوي لا يمكن أن يتجاوز 100 حرف")]
        public string? OneTimeSupplierName { get; set; }

        [Display(Name = "هاتف المورد اليدوي")]
        [StringLength(30, ErrorMessage = "هاتف المورد اليدوي لا يمكن أن يتجاوز 30 حرف")]
        public string? OneTimeSupplierPhone { get; set; }

        // <<< تم إضافة هذا >>>
        [Display(Name = "العميل (في حالة المرتجع)")]
        [Range(1, int.MaxValue, ErrorMessage = "العميل المحدد غير صالح")]
        public int? ClientId { get; set; }

        [Required(ErrorMessage = "أضف عناصر الفاتورة")]
        public List<PurchaseInvoiceItemCreateModel> Items { get; set; } = new();

        [Display(Name = "المبلغ المسدد/المسترجع")]
        [Range(0, double.MaxValue, ErrorMessage = "المبلغ المسدد لا يمكن أن يكون سالباً")]
        public decimal PaidAmount { get; set; } // هذا المبلغ سيتم خصمه من رصيد المورد أو إضافته لرصيد العميل
        [Display(Name = "مبلغ الخصم")]
        [Range(0, double.MaxValue, ErrorMessage = "مبلغ الخصم لا يمكن أن يكون سالباً")]
        public decimal DiscountAmount { get; set; } = 0;
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SupplierId.HasValue && ClientId.HasValue)
            {
                yield return new ValidationResult(
                    "لا يمكن اختيار مورد وعميل في نفس العملية. اختر نوع عملية واحد فقط.",
                    new[] { nameof(SupplierId), nameof(ClientId) });
            }

            if (PartyMode == PurchaseInvoicePartyMode.RegisteredSupplier)
            {
                if (!SupplierId.HasValue || SupplierId.Value <= 0)
                {
                    yield return new ValidationResult(
                        "يجب اختيار مورد مسجل لعملية الشراء.",
                        new[] { nameof(SupplierId) });
                }

                if (ClientId.HasValue)
                {
                    yield return new ValidationResult(
                        "لا يمكن اختيار عميل في عملية شراء من مورد.",
                        new[] { nameof(ClientId) });
                }
            }
            else if (PartyMode == PurchaseInvoicePartyMode.OneTimeSupplier)
            {
                if (SupplierId.HasValue || ClientId.HasValue)
                {
                    yield return new ValidationResult(
                        "المورد اليدوي لا يستخدم رقم مورد أو عميل مسجل.",
                        new[] { nameof(SupplierId), nameof(ClientId) });
                }

                if (string.IsNullOrWhiteSpace(OneTimeSupplierName))
                {
                    yield return new ValidationResult(
                        "يجب إدخال اسم المورد اليدوي.",
                        new[] { nameof(OneTimeSupplierName) });
                }
            }
            else if (PartyMode == PurchaseInvoicePartyMode.RegisteredClientReturn)
            {
                if (!ClientId.HasValue || ClientId.Value <= 0)
                {
                    yield return new ValidationResult(
                        "يجب اختيار عميل مسجل لعملية المرتجع.",
                        new[] { nameof(ClientId) });
                }

                if (SupplierId.HasValue)
                {
                    yield return new ValidationResult(
                        "لا يمكن اختيار مورد في عملية مرتجع من عميل.",
                        new[] { nameof(SupplierId) });
                }
            }
            else
            {
                yield return new ValidationResult(
                    "نوع العملية غير صالح.",
                    new[] { nameof(PartyMode) });
            }
        }
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
    }
}
