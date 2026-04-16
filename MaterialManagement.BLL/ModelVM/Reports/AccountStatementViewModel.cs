using System;

namespace MaterialManagement.BLL.ModelVM.Reports
{
    public class AccountStatementViewModel
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty; // "فاتورة بيع", "تحصيل"
        public string CauseLabel { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string EffectLabel { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty; // رقم الفاتورة أو معرف الدفعة
        public decimal Debit { get; set; } // مدين (المبلغ المطلوب منه - يزيد الدين)
        public decimal Credit { get; set; } 
        public decimal Balance { get; set; } 
        public int? DocumentId { get; set; }
        public string? DocumentType { get; set; }
        public List<TransactionItemViewModel> Items { get; set; } = new List<TransactionItemViewModel>();
    }
    public class TransactionItemViewModel
    {
        public string MaterialName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;

        // --- أضف هذه الخاصية الجديدة ---
        public decimal UnitPrice { get; set; }
    }
}
