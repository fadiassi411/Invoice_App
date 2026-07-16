using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartNumber",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Warranty",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalCharges",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CustomerReference",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryTerms",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverallDiscount",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectLocation",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundingAdjustment",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SourceQuotationId",
                table: "Invoices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceQuotationNumber",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxMode",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Warranty",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowQuotationBelowCost",
                table: "CompanySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowQuotationPriceOverride",
                table: "CompanySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DefaultQuotationDeliveryTerms",
                table: "CompanySettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultQuotationPaymentTerms",
                table: "CompanySettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultQuotationPdfFolder",
                table: "CompanySettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultQuotationTaxMode",
                table: "CompanySettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DefaultQuotationValidityDays",
                table: "CompanySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DefaultQuotationWarranty",
                table: "CompanySettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OpenQuotationPdfAfterExport",
                table: "CompanySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PreventQuotationAboveAvailableStock",
                table: "CompanySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "QuotationFooter",
                table: "CompanySettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "QuotationPrefix",
                table: "CompanySettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "QuotationStartingSequence",
                table: "CompanySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "QuotationTermsAndConditions",
                table: "CompanySettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ResetQuotationSequenceYearly",
                table: "CompanySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowAvailableStockOnPrintedQuotation",
                table: "CompanySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowAvailableStockOnQuotationScreen",
                table: "CompanySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Quotations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuotationNumber = table.Column<string>(type: "TEXT", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentQuotationId = table.Column<int>(type: "INTEGER", nullable: true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuotationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    TaxMode = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerNameSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerCompanySnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    BillingAddressSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    ProjectAddressSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    ContactPersonSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    TelephoneSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    MobileSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    EmailSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    TaxNumberSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    CustomerReference = table.Column<string>(type: "TEXT", nullable: true),
                    ProjectName = table.Column<string>(type: "TEXT", nullable: true),
                    ProjectLocation = table.Column<string>(type: "TEXT", nullable: true),
                    Subject = table.Column<string>(type: "TEXT", nullable: true),
                    Salesperson = table.Column<string>(type: "TEXT", nullable: true),
                    PaymentTerms = table.Column<string>(type: "TEXT", nullable: true),
                    DeliveryTerms = table.Column<string>(type: "TEXT", nullable: true),
                    DeliveryPeriod = table.Column<string>(type: "TEXT", nullable: true),
                    Warranty = table.Column<string>(type: "TEXT", nullable: true),
                    PublicNotes = table.Column<string>(type: "TEXT", nullable: true),
                    InternalNotes = table.Column<string>(type: "TEXT", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "TEXT", nullable: true),
                    Subtotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    LineDiscountTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    OverallDiscountType = table.Column<int>(type: "INTEGER", nullable: false),
                    OverallDiscountValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    OverallDiscountAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    NetAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ShippingCharge = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    AdditionalCharges = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    RoundingAdjustment = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    AmountInWords = table.Column<string>(type: "TEXT", nullable: true),
                    ConvertedInvoiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    ConvertedInvoiceNumber = table.Column<string>(type: "TEXT", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotations_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Quotations_Invoices_ConvertedInvoiceId",
                        column: x => x.ConvertedInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Quotations_Quotations_ParentQuotationId",
                        column: x => x.ParentQuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuotationItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuotationId = table.Column<int>(type: "INTEGER", nullable: false),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemType = table.Column<int>(type: "INTEGER", nullable: false),
                    StockItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    ItemReferenceSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    PartNumberSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    BarcodeSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    DescriptionSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    UnitSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    BrandSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    ManufacturerSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    WarrantySnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    AvailableStockSnapshot = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    DiscountType = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    TaxPercentage = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    NetAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    LineTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationItems_Products_StockItemId",
                        column: x => x.StockItemId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuotationItems_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationStatusHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuotationId = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    NewStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationStatusHistory_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AllowQuotationBelowCost", "AllowQuotationPriceOverride", "DefaultQuotationDeliveryTerms", "DefaultQuotationPaymentTerms", "DefaultQuotationPdfFolder", "DefaultQuotationTaxMode", "DefaultQuotationValidityDays", "DefaultQuotationWarranty", "OpenQuotationPdfAfterExport", "PreventQuotationAboveAvailableStock", "QuotationFooter", "QuotationPrefix", "QuotationStartingSequence", "QuotationTermsAndConditions", "ResetQuotationSequenceYearly", "ShowAvailableStockOnPrintedQuotation", "ShowAvailableStockOnQuotationScreen" },
                values: new object[] { false, true, null, null, null, "Exclusive", 30, null, true, false, "This quotation is subject to the terms and conditions stated herein and remains valid until the indicated validity date.", "QUO", 1, null, true, false, true });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CompanyName", "DeliveryAddress" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CompanyName", "DeliveryAddress" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Manufacturer", "PartNumber", "Warranty" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Manufacturer", "PartNumber", "Warranty" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SourceQuotationId",
                table: "Invoices",
                column: "SourceQuotationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItems_QuotationId_DisplayOrder",
                table: "QuotationItems",
                columns: new[] { "QuotationId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItems_StockItemId",
                table: "QuotationItems",
                column: "StockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_ConvertedInvoiceId",
                table: "Quotations",
                column: "ConvertedInvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_CustomerId_QuotationDate",
                table: "Quotations",
                columns: new[] { "CustomerId", "QuotationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_ParentQuotationId",
                table: "Quotations",
                column: "ParentQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations",
                column: "QuotationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuotationNumber_RevisionNumber",
                table: "Quotations",
                columns: new[] { "QuotationNumber", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_Status_ValidUntil",
                table: "Quotations",
                columns: new[] { "Status", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationStatusHistory_QuotationId_ChangedAt",
                table: "QuotationStatusHistory",
                columns: new[] { "QuotationId", "ChangedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Quotations_SourceQuotationId",
                table: "Invoices",
                column: "SourceQuotationId",
                principalTable: "Quotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Quotations_SourceQuotationId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "QuotationItems");

            migrationBuilder.DropTable(
                name: "QuotationStatusHistory");

            migrationBuilder.DropTable(
                name: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SourceQuotationId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PartNumber",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Warranty",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AdditionalCharges",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerReference",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DeliveryTerms",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "OverallDiscount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaymentTerms",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ProjectLocation",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RoundingAdjustment",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SourceQuotationId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SourceQuotationNumber",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TaxMode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Warranty",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AllowQuotationBelowCost",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "AllowQuotationPriceOverride",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "DefaultQuotationDeliveryTerms",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "DefaultQuotationPaymentTerms",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "DefaultQuotationPdfFolder",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "DefaultQuotationTaxMode",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "DefaultQuotationValidityDays",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "DefaultQuotationWarranty",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "OpenQuotationPdfAfterExport",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "PreventQuotationAboveAvailableStock",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "QuotationFooter",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "QuotationPrefix",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "QuotationStartingSequence",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "QuotationTermsAndConditions",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ResetQuotationSequenceYearly",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ShowAvailableStockOnPrintedQuotation",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ShowAvailableStockOnQuotationScreen",
                table: "CompanySettings");
        }
    }
}
