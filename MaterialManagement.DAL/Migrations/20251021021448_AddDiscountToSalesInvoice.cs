using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountToSalesInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SalesInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "FulfilledQuantity",
                table: "ReservationItems",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SalesInvoices");

            migrationBuilder.AlterColumn<decimal>(
                name: "FulfilledQuantity",
                table: "ReservationItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);
        }
    }
}
