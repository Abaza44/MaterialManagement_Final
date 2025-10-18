using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.ModelVM.Invoice
{
    public class ClientInvoiceSummaryViewModel
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalDebt { get; set; } 
    }
}
