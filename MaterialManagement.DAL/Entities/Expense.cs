using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // <-- أضف هذا

namespace MaterialManagement.DAL.Entities
{
    public class Expense
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime ExpenseDate { get; set; }

        [StringLength(50)]
        public string? Category { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(50)]
        public string? PaymentMethod { get; set; }
        // <<< الخصائص المحدثة هنا >>>
        public int? EmployeeId { get; set; }

        [StringLength(150)]
        public string? PaymentTo { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        // <<< الـ Navigation Property المهم هنا >>>
        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }
    }
}