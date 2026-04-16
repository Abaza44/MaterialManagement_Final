using System;
using System.Collections.Generic;

namespace MaterialManagement.Models
{
    public class SalesReturnSandboxCreateViewModel
    {
        public int? SalesInvoiceId { get; set; }

        public string? InvoiceNumber { get; set; }

        public string? ClientName { get; set; }

        public DateTime? InvoiceDate { get; set; }

        public decimal InvoiceGrossAmount { get; set; }

        public decimal InvoiceDiscountAmount { get; set; }

        public decimal InvoiceNetAmount { get; set; }

        public DateTime ReturnDate { get; set; } = DateTime.Now;

        public string? Notes { get; set; }

        public List<SalesReturnSandboxInvoiceItemViewModel> Items { get; set; } = new();
    }

    public class SalesReturnSandboxInvoiceItemViewModel
    {
        public int SalesInvoiceItemId { get; set; }

        public string? MaterialCode { get; set; }

        public string? MaterialName { get; set; }

        public decimal QuantitySold { get; set; }

        public decimal QuantityAlreadyReturned { get; set; }

        public decimal QuantityRemaining { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal NetUnitPrice { get; set; }

        public decimal ReturnedQuantity { get; set; }
    }
}
