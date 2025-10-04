using System;
using System.Collections.Generic; // <-- أضف هذا
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaterialManagement.DAL.Entities
{
    public class Equipment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Code { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public DateTime PurchaseDate { get; set; }

        // --- تم حذف هذه الخصائص ---
        // public DateTime? LastMaintenanceDate { get; set; }
        // public DateTime? NextMaintenanceDate { get; set; }

        // --- وأضفنا هذه العلاقة بدلاً منها ---
        public virtual ICollection<MaintenanceRecord> MaintenanceHistory { get; set; } = new List<MaintenanceRecord>();
    }
}