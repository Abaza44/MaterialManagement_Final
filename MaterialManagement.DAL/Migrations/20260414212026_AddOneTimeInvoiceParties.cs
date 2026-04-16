using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddOneTimeInvoiceParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Clients_ClientId",
                table: "SalesInvoices");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "SalesInvoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "OneTimeCustomerName",
                table: "SalesInvoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OneTimeCustomerPhone",
                table: "SalesInvoices",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartyMode",
                table: "SalesInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OneTimeSupplierName",
                table: "PurchaseInvoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OneTimeSupplierPhone",
                table: "PurchaseInvoices",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartyMode",
                table: "PurchaseInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE SalesInvoices
                SET PartyMode = 1
                WHERE ClientId IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE PurchaseInvoices
                SET PartyMode = 1
                WHERE SupplierId IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE PurchaseInvoices
                SET PartyMode = 3
                WHERE SupplierId IS NULL
                    AND ClientId IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM SalesInvoices WHERE PartyMode IS NULL)
                    THROW 51000, 'Cannot backfill SalesInvoices.PartyMode because some existing rows do not have a ClientId.', 1;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM PurchaseInvoices WHERE PartyMode IS NULL)
                    THROW 51001, 'Cannot backfill PurchaseInvoices.PartyMode because some existing rows do not have a SupplierId or ClientId.', 1;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "PartyMode",
                table: "SalesInvoices",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PartyMode",
                table: "PurchaseInvoices",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Clients_ClientId",
                table: "SalesInvoices",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Clients_ClientId",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "OneTimeCustomerName",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "OneTimeCustomerPhone",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PartyMode",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "OneTimeSupplierName",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "OneTimeSupplierPhone",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PartyMode",
                table: "PurchaseInvoices");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "SalesInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Clients_ClientId",
                table: "SalesInvoices",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
