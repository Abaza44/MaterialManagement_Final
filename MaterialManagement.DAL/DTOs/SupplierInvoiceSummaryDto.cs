namespace MaterialManagement.DAL.DTOs
{
    public class PurchaseInvoiceSummaryDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal RemainingAmount { get; set; }
    }

    public class SupplierInvoicesDto
    {
        
            public int SupplierId { get; set; }
            public string SupplierName { get; set; }
            public int InvoiceCount { get; set; }
            public decimal TotalCredit { get; set; } // إجمالي المستحقات
        
    }
}