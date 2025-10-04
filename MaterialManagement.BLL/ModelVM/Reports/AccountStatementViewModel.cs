using System;

namespace MaterialManagement.BLL.ModelVM.Reports
{
    public class AccountStatementViewModel
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } // "فاتورة بيع", "تحصيل"
        public string Reference { get; set; } // رقم الفاتورة أو معرف الدفعة
        public decimal Debit { get; set; } // مدين (المبلغ المطلوب منه - يزيد الدين)
        public decimal Credit { get; set; } // دائن (المبلغ الذي دفعه - يقلل الدين)
        public decimal Balance { get; set; } // الرصيد بعد الحركة
    }
}