using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Entities
{
    public class ReservationItem
    {
        public int Id { get; set; }
        public int ReservationId { get; set; }
        public virtual Reservation Reservation { get; set; }

        public int MaterialId { get; set; }
        public virtual Material Material { get; set; }

        public decimal Quantity { get; set; } // الكمية المحجوزة
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal? FulfilledQuantity { get; set; } = 0; 
    }
}
