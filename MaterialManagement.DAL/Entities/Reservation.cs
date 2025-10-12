using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MaterialManagement.DAL.Enums;
namespace MaterialManagement.DAL.Entities
{
    public class Reservation
    {
        public int Id { get; set; }
        public string ReservationNumber { get; set; }
        public DateTime ReservationDate { get; set; } = DateTime.Now;

        public int ClientId { get; set; }
        public virtual Client Client { get; set; }
        
        public decimal TotalAmount { get; set; }
        public ReservationStatus Status { get; set; }
        public string? Notes { get; set; }
        public virtual ICollection<ReservationItem> ReservationItems { get; set; } = new HashSet<ReservationItem>();
    }

}
