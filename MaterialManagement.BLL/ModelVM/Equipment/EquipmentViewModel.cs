using MaterialManagement.BLL.ModelVM.Maintenance;
using System;
using System.ComponentModel.DataAnnotations;

namespace MaterialManagement.BLL.ModelVM.Equipment
{
    public class EquipmentViewModel
    {
        public int Code { get; set; }
        public string Name { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime? LastMaintenanceDate { get; set; }
        public DateTime? NextMaintenanceDate { get; set; }
        public List<MaintenanceRecordViewModel> MaintenanceHistory { get; set; } = new List<MaintenanceRecordViewModel>();
    }

    public class EquipmentCreateModel // لا يوجد Code هنا
    {
        [Required(ErrorMessage = "اسم المعدة مطلوب")]
        [Display(Name = "اسم المعدة")]
        public string Name { get; set; }

        [Required(ErrorMessage = "تاريخ الشراء مطلوب")]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ الشراء")]
        public DateTime PurchaseDate { get; set; } = DateTime.Now;

        [DataType(DataType.Date)]
        [Display(Name = "آخر صيانة (اختياري)")]
        public DateTime? LastMaintenanceDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "الصيانة القادمة (اختياري)")]
        public DateTime? NextMaintenanceDate { get; set; }
    }

    public class EquipmentUpdateModel
    {
        public int Code { get; set; }
        [Required(ErrorMessage = "اسم المعدة مطلوب")]
        [Display(Name = "اسم المعدة")]
        public string Name { get; set; }

        [Required(ErrorMessage = "تاريخ الشراء مطلوب")]
        [Display(Name = "تاريخ الشراء")]
        public DateTime PurchaseDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "آخر صيانة (اختياري)")]
        public DateTime? LastMaintenanceDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "الصيانة القادمة (اختياري)")]
        public DateTime? NextMaintenanceDate { get; set; }
    }
}