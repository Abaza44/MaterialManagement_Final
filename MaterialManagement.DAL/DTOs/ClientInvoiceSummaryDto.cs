namespace MaterialManagement.DAL.DTOs
{

    public class ClientInvoiceSummaryDto
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalDebt { get; set; }
    }

}