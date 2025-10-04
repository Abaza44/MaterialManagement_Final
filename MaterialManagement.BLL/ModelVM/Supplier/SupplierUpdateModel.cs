using System.ComponentModel.DataAnnotations;

namespace MaterialManagement.BLL.ModelVM.Supplier
{
    public class SupplierUpdateModel
    {
        [Required(ErrorMessage = "اسم المورد مطلوب")]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(15)]
        public string? Phone { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        public decimal Balance { get; set; }
        public bool IsActive { get; set; } = true;
    }
}