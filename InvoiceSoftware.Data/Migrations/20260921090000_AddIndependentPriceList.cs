using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Data.Migrations;

[DbContext(typeof(InvoiceDbContext))]
[Migration("20260921090000_AddIndependentPriceList")]
public sealed class AddIndependentPriceList : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PriceListItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ReferenceNumber = table.Column<string>(type: "TEXT", nullable: false),
                ProductName = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                Category = table.Column<string>(type: "TEXT", nullable: true),
                Brand = table.Column<string>(type: "TEXT", nullable: true),
                Unit = table.Column<string>(type: "TEXT", nullable: false),
                SellingPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                TaxRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                ImagePath = table.Column<string>(type: "TEXT", nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                ModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_PriceListItems", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_PriceListItems_ProductName",
            table: "PriceListItems",
            column: "ProductName");

        migrationBuilder.CreateIndex(
            name: "IX_PriceListItems_ReferenceNumber",
            table: "PriceListItems",
            column: "ReferenceNumber",
            unique: true);

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PriceListItems");
    }
}
