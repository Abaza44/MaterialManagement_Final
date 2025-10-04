using System.ComponentModel.DataAnnotations;

namespace MaterialManagement.BLL.ModelVM.Material
{
    public class MaterialViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public decimal MinimumQuantity { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }

        // Calculated Properties
        public decimal TotalValue => Quantity * PurchasePrice;
        public decimal ProfitMargin => SellingPrice - PurchasePrice;
        public string StockStatus => Quantity <= 10 ? "منخفض" : Quantity <= 50 ? "متوسط" : "جيد";
    }
}