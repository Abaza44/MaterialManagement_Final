using System;
using System.Collections.Generic;

namespace MaterialManagement.BLL.ModelVM.Invoice
{
    public class PurchaseInvoiceViewModel
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public int? ClientId { get; set; }
        public string? ClientName { get; set; } 
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string? Notes { get; set; }
        public List<PurchaseInvoiceItemViewModel> Items { get; set; } = new();
    }

    public class PurchaseInvoiceItemViewModel
    {
        public int MaterialId { get; set; }
        public string MaterialCode { get; set; } // <-- تم إضافته
        public string MaterialName { get; set; } // <-- تم إضافته
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}