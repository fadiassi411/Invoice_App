using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InvoiceSoftware.Data.Migrations;

[DbContext(typeof(InvoiceDbContext))]
[Migration("20260920090000_AddProductImagePath")]
public sealed class AddProductImagePath : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(
            name: "ImagePath", table: "Products", type: "TEXT", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "ImagePath", table: "Products");
}
