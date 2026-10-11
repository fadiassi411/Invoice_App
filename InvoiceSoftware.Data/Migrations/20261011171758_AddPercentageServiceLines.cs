using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPercentageServiceLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ServicePercentage",
                table: "QuotationItems",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ServicePercentage",
                table: "InvoiceItems",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServicePercentage",
                table: "QuotationItems");

            migrationBuilder.DropColumn(
                name: "ServicePercentage",
                table: "InvoiceItems");
        }
    }
}
