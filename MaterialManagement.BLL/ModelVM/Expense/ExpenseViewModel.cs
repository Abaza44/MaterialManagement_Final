using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.ModelVM.Expense
{
    public class ExpenseViewModel
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string? Category { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? PaymentTo { get; set; }
        public bool IsActive { get; set; }

        // <<< أضف هذا السطر هنا >>>
        public DateTime CreatedDate { get; set; }
    }
}
