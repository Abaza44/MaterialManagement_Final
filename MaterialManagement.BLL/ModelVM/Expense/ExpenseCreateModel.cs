using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.ModelVM.Expense
{
    public class ExpenseCreateModel
    {
        [Required(ErrorMessage = "الوصف مطلوب")][Display(Name = "وصف المصروف")] public string Description { get; set; }
        [Required(ErrorMessage = "المبلغ مطلوب")][Display(Name = "المبلغ")] public decimal Amount { get; set; }
        [Required(ErrorMessage = "التاريخ مطلوب")][DataType(DataType.Date)][Display(Name = "تاريخ المصروف")] public DateTime ExpenseDate { get; set; } = DateTime.Now;
        [Display(Name = "الفئة")] public string? Category { get; set; }

        // <<< أضف هذا السطر >>>
        [Display(Name = "طريقة الدفع")] public string? PaymentMethod { get; set; }

        [Display(Name = "ملاحظات")] public string? Notes { get; set; }
        [Display(Name = "الموظف (إذا كان راتب)")] public int? EmployeeId { get; set; }
        [Display(Name = "مدفوع إلى")] public string? PaymentTo { get; set; }
    }
}
