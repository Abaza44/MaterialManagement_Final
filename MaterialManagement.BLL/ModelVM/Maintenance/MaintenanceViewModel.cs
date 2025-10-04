using System;
using System.ComponentModel.DataAnnotations;

namespace MaterialManagement.BLL.ModelVM.Maintenance
{
    public class MaintenanceRecordViewModel
    {
        public int Id { get; set; }
        public int EquipmentCode { get; set; }
        public string EquipmentName { get; set; }
        public DateTime MaintenanceDate { get; set; }
        public string Description { get; set; }
        public string? PerformedBy { get; set; }
        public decimal Cost { get; set; }
        public string? Notes { get; set; }
    }

    public class MaintenanceRecordCreateModel
    {
        [Required]
        public int EquipmentCode { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ الصيانة")]
        public DateTime MaintenanceDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "وصف الصيانة مطلوب")]
        [StringLength(200)]
        [Display(Name = "وصف الصيانة")]
        public string Description { get; set; }

        [StringLength(100)]
        [Display(Name = "تم بواسطة (الفني/الشركة)")]
        public string? PerformedBy { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "التكلفة يجب أن تكون رقمًا موجبًا")]
        [Display(Name = "التكلفة")]
        public decimal Cost { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات إضافية")]
        public string? Notes { get; set; }
    }
}