using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaterialManagement.DAL.Entities
{
    public class MaintenanceRecord
    {
        [Key]
        public int Id { get; set; } // مفتاح أساسي خاص بسجل الصيانة

        [Required]
        public int EquipmentCode { get; set; } // مفتاح خارجي يربط بالمعدة

        [Required]
        public DateTime MaintenanceDate { get; set; }

        [Required]
        [StringLength(200)]
        public string Description { get; set; } // وصف الصيانة (مثال: تغيير زيت, إصلاح عطل كهربائي)

        [StringLength(100)]
        public string? PerformedBy { get; set; } // اسم الفني أو الشركة

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Cost { get; set; } // تكلفة الصيانة

        [StringLength(500)]
        public string? Notes { get; set; } // ملاحظات إضافية

        // علاقة الربط (Navigation Property)
        [ForeignKey("EquipmentCode")]
        public virtual Equipment Equipment { get; set; }
    }
}