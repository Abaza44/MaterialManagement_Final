using System;
using System.ComponentModel.DataAnnotations;

namespace MaterialManagement.BLL.ModelVM.Employee
{
    public class EmployeeViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Phone { get; set; }
        public decimal Salary { get; set; }
        public DateTime HireDate { get; set; }
        public string? Position { get; set; }
        public bool IsActive { get; set; }
    }

    public class EmployeeCreateModel
    {
        [Required(ErrorMessage = "اسم الموظف مطلوب")]
        [Display(Name = "اسم الموظف")]
        public string Name { get; set; }

        [Display(Name = "رقم الهاتف")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "الراتب مطلوب")]
        [Display(Name = "الراتب الشهري")]
        [Range(0, double.MaxValue)]
        public decimal Salary { get; set; }

        [Required(ErrorMessage = "تاريخ التعيين مطلوب")]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ التعيين")]
        public DateTime HireDate { get; set; } = DateTime.Now;

        [Display(Name = "الوظيفة")]
        public string? Position { get; set; }
    }

    public class EmployeeUpdateModel
    {
        public int Id { get; set; }
        [Required][Display(Name = "اسم الموظف")] public string Name { get; set; }
        [Display(Name = "رقم الهاتف")] public string? Phone { get; set; }
        [Required][Display(Name = "الراتب")] public decimal Salary { get; set; }
        [Required][DataType(DataType.Date)][Display(Name = "تاريخ التعيين")] public DateTime HireDate { get; set; }
        [Display(Name = "الوظيفة")] public string? Position { get; set; }
        [Display(Name = "نشط؟")] public bool IsActive { get; set; }
    }
}