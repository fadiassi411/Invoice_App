using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierPriceListImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PriceListItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "PriceListItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierPriceListMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    SheetName = table.Column<string>(type: "TEXT", nullable: false),
                    HeaderRowNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    HeaderNamesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ColumnMappingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ParsingSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPriceListMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPriceListMappings_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItems_SupplierId",
                table: "PriceListItems",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPriceListMappings_SupplierId",
                table: "SupplierPriceListMappings",
                column: "SupplierId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceListItems_Suppliers_SupplierId",
                table: "PriceListItems",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PriceListItems_Suppliers_SupplierId",
                table: "PriceListItems");

            migrationBuilder.DropTable(
                name: "SupplierPriceListMappings");

            migrationBuilder.DropIndex(
                name: "IX_PriceListItems_SupplierId",
                table: "PriceListItems");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PriceListItems");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "PriceListItems");
        }
    }
}
