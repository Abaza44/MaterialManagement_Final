using System;

namespace MaterialManagement.BLL.ModelVM.Reports
{
    public class ProfitReportViewModel
    {
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string ClientName { get; set; }
        public decimal TotalAmount { get; set; } // إجمالي البيع
        public decimal TotalCost { get; set; } // إجمالي التكلفة (سعر الشراء)
        public decimal Profit { get; set; } // الربح
    }
}