using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountToPurchaseInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "PurchaseInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "PurchaseInvoices");
        }
    }
}
