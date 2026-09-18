using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;
using InvoiceSoftware.Data;
using InvoiceSoftware.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InvoiceSoftware.Tests;

public sealed class ProfitEstimateTests
{
    [Fact]
    public void Quotation_estimate_excludes_tax_and_shipping_and_includes_hidden_lines()
    {
        var quotation = new Quotation
        {
            ShippingCharge = 12m,
            OverallDiscountValue = 10m,
            Items =
            [
                new QuotationItem { ItemType = QuotationItemType.StockItem, Quantity = 2, UnitPrice = 100m, CostPriceSnapshot = 60m, TaxPercentage = 10m },
                new QuotationItem { ItemType = QuotationItemType.ManualItem, Quantity = 1, UnitPrice = 50m, CostPriceSnapshot = 20m, HideOnPdf = true }
            ]
        };
        new QuotationCalculator().Calculate(quotation);

        var estimate = ProfitEstimateCalculator.ForQuotation(quotation);

        Assert.Equal(225m, estimate.NetSales);
        Assert.Equal(140m, estimate.KnownCost);
        Assert.Equal(85m, estimate.GrossProfit);
        Assert.Equal(37.78m, estimate.MarginPercent);
    }

    [Fact]
    public void Invoice_estimate_excludes_tax_shipping_and_previous_balance()
    {
        var invoice = new Invoice
        {
            ShippingCharges = 10m,
            PreviousBalance = 40m,
            OverallDiscount = 5m,
            Items = [new InvoiceItem { Quantity = 2, UnitPrice = 100m, CostPriceSnapshot = 60m, DiscountPercentage = 10m, TaxPercentage = 5m }]
        };
        new InvoiceCalculator().Calculate(invoice);

        var estimate = ProfitEstimateCalculator.ForInvoice(invoice);

        Assert.Equal(175m, estimate.NetSales);
        Assert.Equal(120m, estimate.KnownCost);
        Assert.Equal(55m, estimate.GrossProfit);
    }

    [Fact]
    public void Unknown_cost_prevents_misleading_profit()
    {
        var quotation = new Quotation { Items = [new QuotationItem { Quantity = 1, UnitPrice = 100m }] };
        new QuotationCalculator().Calculate(quotation);
        var invoice = new Invoice { Items = [new InvoiceItem { Quantity = 1, UnitPrice = 100m, CostPriceSnapshot = 0m }] };
        new InvoiceCalculator().Calculate(invoice);

        Assert.Null(ProfitEstimateCalculator.ForQuotation(quotation).GrossProfit);
        Assert.Null(ProfitEstimateCalculator.ForInvoice(invoice).GrossProfit);
        Assert.Equal(1, ProfitEstimateCalculator.ForQuotation(quotation).MissingCostLines);
    }

    [Fact]
    public void Inclusive_tax_is_not_profit()
    {
        var quotation = new Quotation
        {
            TaxMode = TaxMode.Inclusive,
            Items = [new QuotationItem { Quantity = 1, UnitPrice = 110m, CostPriceSnapshot = 60m, TaxPercentage = 10m }]
        };
        new QuotationCalculator().Calculate(quotation);

        Assert.Equal(40m, ProfitEstimateCalculator.ForQuotation(quotation).GrossProfit);
    }

    [Fact]
    public async Task Quotation_cost_is_saved_and_preserved_in_a_revision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new InvoiceDbContext(new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var service = new QuotationService(db, new ReferenceNumberService(db), new QuotationCalculator(), new InvoiceCalculator());
        var quotation = await service.CreateDraftAsync(1);
        quotation.Items.Add(new QuotationItem
        {
            ItemType = QuotationItemType.ManualItem, DescriptionSnapshot = "Installation", Quantity = 2,
            UnitPrice = 120m, CostPriceSnapshot = 75m
        });
        await service.SaveAsync(quotation);
        db.ChangeTracker.Clear();

        var saved = await service.GetAsync(quotation.Id);
        Assert.Equal(75m, Assert.Single(saved!.Items).CostPriceSnapshot);

        var revision = await service.CreateRevisionAsync(quotation.Id);
        Assert.Equal(75m, Assert.Single(revision.Items).CostPriceSnapshot);
    }

    [Fact]
    public async Task Existing_quotation_database_can_add_nullable_internal_cost()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new InvoiceDbContext(new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260916080458_AddQuotationPdfVisibility");
        await migrator.MigrateAsync();

        var columnCount = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('QuotationItems') WHERE name = 'CostPriceSnapshot'").SingleAsync();
        Assert.Equal(1, columnCount);
    }
}
