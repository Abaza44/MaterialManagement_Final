using System;

namespace MaterialManagement.BLL.ModelVM.Reports
{
    public class AccountStatementViewModel
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } // "فاتورة بيع", "تحصيل"
        public string Reference { get; set; } // رقم الفاتورة أو معرف الدفعة
        public decimal Debit { get; set; } // مدين (المبلغ المطلوب منه - يزيد الدين)
        public decimal Credit { get; set; } 
        public decimal Balance { get; set; } 
        public int? DocumentId { get; set; }
        public string? DocumentType { get; set; }
        public List<TransactionItemViewModel> Items { get; set; } = new List<TransactionItemViewModel>();
    }
    public class TransactionItemViewModel
    {
        public string MaterialName { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }

        // --- أضف هذه الخاصية الجديدة ---
        public decimal UnitPrice { get; set; }
    }
}