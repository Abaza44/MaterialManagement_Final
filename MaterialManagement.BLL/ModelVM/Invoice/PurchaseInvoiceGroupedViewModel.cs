using MaterialManagement.BLL.ModelVM.Payment;
using System;
using System.Collections.Generic;

namespace MaterialManagement.BLL.ModelVM.Invoice
{

    public class PurchaseInvoiceSummaryViewModel
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal RemainingAmount { get; set; }
    }
    public class SupplierInvoiceSummaryViewModel
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalCredit { get; set; }
    }
    public class SupplierInvoicesViewModel
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public List<PurchaseInvoiceSummaryViewModel> Invoices { get; set; } = new();
    }

    public class PurchaseInvoiceDetailsViewModel
    {
        public PurchaseInvoiceViewModel Invoice { get; set; }
        public List<SupplierPaymentViewModel> Payments { get; set; } = new();
    }
}