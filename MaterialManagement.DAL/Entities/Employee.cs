using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // <-- أضف هذا

namespace MaterialManagement.DAL.Entities
{
    public class Employee
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // <<< هذا هو السطر المهم
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الموظف مطلوب")]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(15)]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "الراتب مطلوب")]
        [Column(TypeName = "decimal(18, 2)")]
        [Range(0, double.MaxValue, ErrorMessage = "الراتب يجب أن يكون رقمًا موجبًا")]
        public decimal Salary { get; set; }

        [Required(ErrorMessage = "تاريخ التعيين مطلوب")]
        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; }

        [StringLength(50)]
        public string? Position { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        [Column(TypeName = "decimal(18, 2)")]
        public decimal MinimumQuantity { get; set; } = 0; // القيمة الافتراضية صفر
    }
}