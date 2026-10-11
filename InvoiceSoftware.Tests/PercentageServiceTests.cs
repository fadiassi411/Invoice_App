using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;
using InvoiceSoftware.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InvoiceSoftware.Tests;

public sealed class PercentageServiceTests
{
    [Theory]
    [InlineData(TaxMode.Exclusive)]
    [InlineData(TaxMode.Inclusive)]
    public void Quotation_service_uses_other_items_including_tax_without_recursion(TaxMode mode)
    {
        var quotation = new Quotation
        {
            TaxMode = mode,
            Items =
            [
                new QuotationItem { LineNumber = 1, DisplayOrder = 1, ItemType = QuotationItemType.ManualItem,
                    DescriptionSnapshot = "Equipment", Quantity = 1, UnitPrice = 100m, TaxPercentage = 10m },
                new QuotationItem { LineNumber = 2, DisplayOrder = 2, ItemType = QuotationItemType.Service,
                    DescriptionSnapshot = "Labour", Quantity = 1, ServicePercentage = 10m, TaxPercentage = 10m },
                new QuotationItem { LineNumber = 3, DisplayOrder = 3, ItemType = QuotationItemType.Service,
                    DescriptionSnapshot = "Commissioning", Quantity = 1, ServicePercentage = 5m, TaxPercentage = 0m }
            ]
        };

        new QuotationCalculator().Calculate(quotation);
        var baseAmount = quotation.Items[0].LineTotal;
        Assert.Equal(Math.Round(baseAmount * 0.10m, 2, MidpointRounding.AwayFromZero), quotation.Items[1].UnitPrice);
        Assert.Equal(Math.Round(baseAmount * 0.05m, 2, MidpointRounding.AwayFromZero), quotation.Items[2].UnitPrice);
        Assert.Equal(quotation.Items.Sum(x => x.LineTotal), quotation.GrandTotal);

        quotation.Items[0].UnitPrice = 200m;
        new QuotationCalculator().Calculate(quotation);
        Assert.Equal(baseAmount * 2m * 0.10m, quotation.Items[1].UnitPrice);
    }

    [Fact]
    public void Invoice_service_uses_other_items_including_tax()
    {
        var invoice = new Invoice
        {
            TaxMode = TaxMode.Exclusive,
            Items =
            [
                new InvoiceItem { SortOrder = 1, Description = "Equipment", Quantity = 1, UnitPrice = 100m,
                    DiscountPercentage = 10m, TaxPercentage = 10m },
                new InvoiceItem { SortOrder = 2, Description = "Labour", Quantity = 1,
                    ServicePercentage = 10m, TaxPercentage = 10m }
            ]
        };

        new InvoiceCalculator().Calculate(invoice);
        Assert.Equal(99m, invoice.Items[0].LineTotal);
        Assert.Equal(9.90m, invoice.Items[1].UnitPrice);
        Assert.Equal(109.89m, invoice.GrandTotal);
        Assert.Throws<InvalidOperationException>(() => { invoice.Items[1].ServicePercentage = 101m; new InvoiceCalculator().Calculate(invoice); });
    }

    [Fact]
    public async Task Existing_database_upgrades_without_losing_document_items()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite(connection).Options;
        await using var db = new InvoiceDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261007115625_AddSupplierPriceListImport");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Customers (CustomerCode, Name, CreditLimit, OpeningBalance, IsActive, CreatedAt, IsDeleted) VALUES ('TEST-UPGRADE', 'Existing', 0, 0, 1, CURRENT_TIMESTAMP, 0)");
        await migrator.MigrateAsync();
        Assert.True(await db.Customers.AnyAsync(x => x.CustomerCode == "TEST-UPGRADE"));
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('InvoiceItems') WHERE name = 'ServicePercentage'").SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('QuotationItems') WHERE name = 'ServicePercentage'").SingleAsync());
    }
}
