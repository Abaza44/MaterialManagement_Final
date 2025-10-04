using System.ComponentModel.DataAnnotations;

namespace MaterialManagement.BLL.ModelVM.Client
{
    public class ClientUpdateModel
    {
        [Required(ErrorMessage = "اسم العميل مطلوب")]
        [StringLength(100, ErrorMessage = "اسم العميل لا يمكن أن يزيد عن 100 حرف")]
        public string Name { get; set; }

        [StringLength(15, ErrorMessage = "رقم الهاتف لا يمكن أن يزيد عن 15 رقم")]
        public string? Phone { get; set; }

        [StringLength(200, ErrorMessage = "العنوان لا يمكن أن يزيد عن 200 حرف")]
        public string? Address { get; set; }

        public decimal Balance { get; set; }
        public bool IsActive { get; set; } = true;
    }
}