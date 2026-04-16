using MaterialManagement.DAL.Entities;
using System;
using System.Collections.Generic;

namespace MaterialManagement.BLL.ModelVM.Returns
{
    public class SalesReturnCreateModel
    {
        public int SalesInvoiceId { get; set; }

        public DateTime ReturnDate { get; set; } = DateTime.Now;

        public string? Notes { get; set; }

        public List<SalesReturnItemCreateModel> Items { get; set; } = new();
    }

    public class SalesReturnItemCreateModel
    {
        public int SalesInvoiceItemId { get; set; }

        public decimal ReturnedQuantity { get; set; }
    }

    public class SalesReturnViewModel
    {
        public int Id { get; set; }

        public string ReturnNumber { get; set; } = string.Empty;

        public int SalesInvoiceId { get; set; }

        public int ClientId { get; set; }

        public DateTime ReturnDate { get; set; }

        public ReturnStatus Status { get; set; }

        public decimal TotalGrossAmount { get; set; }

        public decimal TotalProratedDiscount { get; set; }

        public decimal TotalNetAmount { get; set; }

        public string? Notes { get; set; }

        public List<SalesReturnItemViewModel> Items { get; set; } = new();
    }

    public class SalesReturnItemViewModel
    {
        public int Id { get; set; }

        public int SalesInvoiceItemId { get; set; }

        public int MaterialId { get; set; }

        public string? MaterialCode { get; set; }

        public string? MaterialName { get; set; }

        public decimal ReturnedQuantity { get; set; }

        public decimal OriginalUnitPrice { get; set; }

        public decimal NetUnitPrice { get; set; }

        public decimal TotalReturnNetAmount { get; set; }
    }
}
