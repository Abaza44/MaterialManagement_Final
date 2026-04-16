using System.ComponentModel.DataAnnotations;

using MaterialManagement.DAL.Enums;

namespace MaterialManagement.BLL.ModelVM.Invoice
{
    public class SalesInvoiceCreateModel : IValidatableObject
    {
        
        [StringLength(50)]
        public string? InvoiceNumber { get; set; }

        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [Display(Name = "نوع العميل")]
        public SalesInvoicePartyMode PartyMode { get; set; } = SalesInvoicePartyMode.RegisteredClient;

        [Display(Name = "العميل")]
        [Range(1, int.MaxValue, ErrorMessage = "العميل المحدد غير صالح")]
        public int? ClientId { get; set; }

        [Display(Name = "اسم العميل النقدي")]
        [StringLength(100, ErrorMessage = "اسم العميل النقدي لا يمكن أن يتجاوز 100 حرف")]
        public string? OneTimeCustomerName { get; set; }

        [Display(Name = "هاتف العميل النقدي")]
        [StringLength(30, ErrorMessage = "هاتف العميل النقدي لا يمكن أن يتجاوز 30 حرف")]
        public string? OneTimeCustomerPhone { get; set; }

        [Required]
        public List<SalesInvoiceItemCreateModel> Items { get; set; } = new();

        [Range(0, double.MaxValue, ErrorMessage = "المبلغ المدفوع لا يمكن أن يكون سالباً")]
        public decimal PaidAmount { get; set; }
        public string? Notes { get; set; }
        [Display(Name = "مبلغ الخصم")]
        [Range(0, double.MaxValue, ErrorMessage = "مبلغ الخصم لا يمكن أن يكون سالباً")]
        public decimal DiscountAmount { get; set; } = 0;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (PartyMode == SalesInvoicePartyMode.RegisteredClient)
            {
                if (!ClientId.HasValue || ClientId.Value <= 0)
                {
                    yield return new ValidationResult(
                        "يجب اختيار عميل مسجل.",
                        new[] { nameof(ClientId) });
                }
            }
            else if (PartyMode == SalesInvoicePartyMode.WalkInCustomer)
            {
                if (string.IsNullOrWhiteSpace(OneTimeCustomerName))
                {
                    yield return new ValidationResult(
                        "يجب إدخال اسم العميل النقدي.",
                        new[] { nameof(OneTimeCustomerName) });
                }
            }
            else
            {
                yield return new ValidationResult(
                    "نوع العميل غير صالح.",
                    new[] { nameof(PartyMode) });
            }
        }
    }

    public class SalesInvoiceItemCreateModel
    {
        [Required]
        public int MaterialId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من الصفر")]
        public decimal Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "سعر الوحدة يجب أن يكون أكبر من الصفر")]
        public decimal UnitPrice { get; set; }
    }
}
