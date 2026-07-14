using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Data.Migrations;

public partial class BackfillLegacyInventoryData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE Products SET Name = Description WHERE Name = ''; ");
        migrationBuilder.Sql("UPDATE Products SET TrackStock = CASE WHEN Type = 1 THEN 0 ELSE 1 END;");
        migrationBuilder.Sql("UPDATE Categories SET IsActive = 1;");
        migrationBuilder.Sql("UPDATE Products SET CategoryId = (SELECT Id FROM Categories WHERE Categories.Name = Products.Category LIMIT 1) WHERE Category IS NOT NULL AND CategoryId IS NULL;");
        migrationBuilder.Sql(@"
            INSERT INTO StockMovements
                (ProductId, ReferenceNumber, TransactionType, QuantityBefore, QuantityChanged, QuantityAfter,
                 CostPriceAtTransaction, RelatedInvoiceId, RelatedInvoiceReferenceNumber, TransactionDate,
                 Operator, Notes, CreatedAt, ModifiedAt, CreatedBy, ModifiedBy, IsDeleted, DeletedAt)
            SELECT Id, Code, 0, 0, CurrentQuantity, CurrentQuantity, CostPrice, NULL, NULL,
                   CURRENT_TIMESTAMP, 'System', 'Opening balance established during inventory upgrade',
                   CURRENT_TIMESTAMP, NULL, NULL, NULL, 0, NULL
            FROM Products WHERE CurrentQuantity <> 0 AND Type <> 1;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM StockMovements WHERE Notes = 'Opening balance established during inventory upgrade' AND RelatedInvoiceId IS NULL;");
    }
}
