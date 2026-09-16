using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationPdfVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HideItemPricesOnPdf",
                table: "Quotations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HideOnPdf",
                table: "QuotationItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HideItemPricesOnPdf",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "HideOnPdf",
                table: "QuotationItems");
        }
    }
}
