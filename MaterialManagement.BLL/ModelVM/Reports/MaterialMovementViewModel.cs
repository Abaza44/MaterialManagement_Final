using System;

namespace MaterialManagement.BLL.ModelVM.Reports
{
    public class MaterialMovementViewModel
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } // "فاتورة شراء", "فاتورة بيع"
        public string InvoiceNumber { get; set; }
        public decimal QuantityIn { get; set; } // الكمية الواردة (شراء)
        public decimal QuantityOut { get; set; } // الكمية الصادرة (بيع)
        public decimal Balance { get; set; } // الرصيد بعد الحركة
    }
}