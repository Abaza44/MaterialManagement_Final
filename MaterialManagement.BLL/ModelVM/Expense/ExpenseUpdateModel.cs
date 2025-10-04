using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.ModelVM.Expense
{
    public class ExpenseUpdateModel
    {
        public int Id { get; set; }
        [Required][Display(Name = "الوصف")] public string Description { get; set; }
        [Required][Display(Name = "المبلغ")] public decimal Amount { get; set; }
        [Required][DataType(DataType.Date)][Display(Name = "التاريخ")] public DateTime ExpenseDate { get; set; }
        [Display(Name = "الفئة")] public string? Category { get; set; }

        // <<< أضف هذا السطر >>>
        [Display(Name = "طريقة الدفع")] public string? PaymentMethod { get; set; }

        [Display(Name = "ملاحظات")] public string? Notes { get; set; }
        [Display(Name = "الموظف")] public int? EmployeeId { get; set; }
        [Display(Name = "مدفوع إلى")] public string? PaymentTo { get; set; }
        [Display(Name = "نشط؟")] public bool IsActive { get; set; }
    }
}
