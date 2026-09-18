using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InvoiceSoftware.Data.Migrations;

[DbContext(typeof(InvoiceDbContext))]
[Migration("20260917090000_AddQuotationInternalCost")]
public sealed class AddQuotationInternalCost : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<decimal>(
            name: "CostPriceSnapshot", table: "QuotationItems", type: "TEXT", precision: 18, scale: 4, nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "CostPriceSnapshot", table: "QuotationItems");
}
