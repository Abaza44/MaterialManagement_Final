using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.ModelVM.Invoice
{
    public class InvoiceSummaryViewModel
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
    }

    public class ClientInvoicesViewModel
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public List<InvoiceSummaryViewModel> Invoices { get; set; } = new();
    }
}
