using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddFulfilledQuantityToReservationItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FulfilledQuantity",
                table: "ReservationItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FulfilledQuantity",
                table: "ReservationItems");
        }
    }
}
