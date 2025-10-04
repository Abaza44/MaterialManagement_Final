using System.ComponentModel.DataAnnotations;

namespace MaterialManagement.BLL.ModelVM.Material
{
    public class MaterialCreateModel
    {
        [Required(ErrorMessage = "اسم المادة مطلوب")]
        [StringLength(100, ErrorMessage = "اسم المادة لا يمكن أن يزيد عن 100 حرف")]
        [Display(Name = "اسم المادة")]
        public string Name { get; set; }
        [Display(Name = "أقل كمية للتنبيه")]
        [Range(0, double.MaxValue)]
        public decimal MinimumQuantity { get; set; } = 0;
        [Required(ErrorMessage = "الكود مطلوب")]
        [StringLength(50, ErrorMessage = "الكود لا يمكن أن يزيد عن 50 حرف")]
        [Display(Name = "الكود")]
        public string Code { get; set; }

        [Required(ErrorMessage = "الوحدة مطلوبة")]
        [StringLength(20, ErrorMessage = "الوحدة لا يمكن أن تزيد عن 20 حرف")]
        [Display(Name = "الوحدة")]
        public string Unit { get; set; }

        [Required(ErrorMessage = "الكمية مطلوبة")]
        [Range(0, double.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من أو تساوي صفر")]
        [Display(Name = "الكمية")]
        public decimal Quantity { get; set; }

        //[Required(ErrorMessage = "سعر الشراء مطلوب")]
        //[Range(0.01, double.MaxValue, ErrorMessage = "سعر الشراء يجب أن يكون أكبر من صفر")]
        //[Display(Name = "سعر الشراء")]
        //public decimal PurchasePrice { get; set; }

        //[Required(ErrorMessage = "سعر البيع مطلوب")]
        //[Range(0.01, double.MaxValue, ErrorMessage = "سعر البيع يجب أن يكون أكبر من صفر")]
        //[Display(Name = "سعر البيع")]
        //public decimal SellingPrice { get; set; }

        [StringLength(500, ErrorMessage = "الوصف لا يمكن أن يزيد عن 500 حرف")]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;
    }
}