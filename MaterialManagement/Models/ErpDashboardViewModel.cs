using System;
using System.Collections.Generic;

namespace MaterialManagement.Models
{
    public class ErpDashboardViewModel
    {
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public DateTime MonthStart { get; set; }
        public DateTime Today { get; set; }

        public decimal ClientReceivables { get; set; }
        public decimal ClientCreditBalances { get; set; }
        public decimal SupplierPayables { get; set; }
        public decimal SupplierCreditBalances { get; set; }
        public decimal EstimatedStockValue { get; set; }
        public int NegativeStockCount { get; set; }
        public int UnavailableStockCount { get; set; }
        public int OpenReservationsCount { get; set; }
        public decimal OpenReservationsValue { get; set; }
        public int UnpaidSalesInvoiceCount { get; set; }
        public decimal UnpaidSalesInvoiceAmount { get; set; }
        public int UnpaidPurchaseInvoiceCount { get; set; }
        public decimal UnpaidPurchaseInvoiceAmount { get; set; }

        public decimal MonthSalesTotal { get; set; }
        public decimal MonthPurchaseTotal { get; set; }
        public decimal MonthExpenseTotal { get; set; }
        public decimal MonthSalesReturnTotal { get; set; }
        public decimal Last7DaysSalesTotal { get; set; }
        public decimal Last7DaysPurchaseTotal { get; set; }

        public List<DashboardAttentionItem> AttentionItems { get; set; } = new();
        public List<DashboardActivityItem> RecentActivities { get; set; } = new();
        public List<DashboardBalanceItem> TopClientBalances { get; set; } = new();
        public List<DashboardBalanceItem> TopSupplierBalances { get; set; } = new();
        public List<DashboardMaterialItem> MaterialsNeedingReview { get; set; } = new();
        public List<DashboardReservationItem> PendingReservations { get; set; } = new();
    }

    public class DashboardAttentionItem
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Tone { get; set; } = "neutral";
        public string Url { get; set; } = "#";
    }

    public class DashboardActivityItem
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Party { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Tone { get; set; } = "neutral";
        public string Url { get; set; } = "#";
    }

    public class DashboardBalanceItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Url { get; set; } = "#";
    }

    public class DashboardMaterialItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public string Url { get; set; } = "#";
    }

    public class DashboardReservationItem
    {
        public int Id { get; set; }
        public string ReservationNumber { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public DateTime ReservationDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Url { get; set; } = "#";
    }
}
