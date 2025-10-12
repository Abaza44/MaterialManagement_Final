using MaterialManagement.DAL.Entities; // For the enum
using MaterialManagement.DAL.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MaterialManagement.BLL.ModelVM.Reservation
{
    public class ReservationIndexViewModel
    {
        public int Id { get; set; }
        [Display(Name = "رقم الحجز")]
        public string ReservationNumber { get; set; }
        [Display(Name = "تاريخ الحجز")]
        public DateTime ReservationDate { get; set; }
        [Display(Name = "العميل")]
        public string ClientName { get; set; }
        [Display(Name = "الإجمالي")]
        public decimal TotalAmount { get; set; }
        [Display(Name = "الحالة")]
        public ReservationStatus Status { get; set; }
    }

    public class ReservationCreateModel
    {
        [Required]
        [Display(Name = "العميل")]
        public int ClientId { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Required]
        public List<ReservationItemModel> Items { get; set; } = new();
    }

    public class ReservationItemModel
    {
        [Required]
        public int MaterialId { get; set; }

        public string? MaterialName { get; set; } // <<< أضف هذا

        [Required, Range(0.01, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Required, Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        public decimal TotalPrice => Quantity * UnitPrice;
    }

    // أضف هذا الكلاس الجديد بالكامل
    public class ReservationDetailsViewModel
    {
        public int Id { get; set; }
        [Display(Name = "رقم الحجز")]
        public string ReservationNumber { get; set; }
        [Display(Name = "تاريخ الحجز")]
        public DateTime ReservationDate { get; set; }
        [Display(Name = "العميل")]
        public string ClientName { get; set; }
        [Display(Name = "الإجمالي")]
        public decimal TotalAmount { get; set; }
        [Display(Name = "الحالة")]
        public ReservationStatus Status { get; set; }
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public List<ReservationItemModel> Items { get; set; } = new();
    }

    public class ReservationSummaryViewModel
    {
        public int Id { get; set; }
        public string ReservationNumber { get; set; }
        public DateTime ReservationDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string ItemsSummary { get; set; } // ملخص الأصناف الذي أنشأناه سابقًا
    }

    /// <summary>
    /// يمثل العميل ومعه قائمة بكل حجوزاته. هذا هو النموذج الرئيسي للصفحة.
    /// </summary>
    public class ClientReservationsViewModel
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public List<ReservationSummaryViewModel> Reservations { get; set; } = new();
    }
}