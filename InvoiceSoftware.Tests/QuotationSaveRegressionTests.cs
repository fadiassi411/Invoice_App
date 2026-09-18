using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;
using InvoiceSoftware.Data;
using InvoiceSoftware.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Tests;

public sealed class QuotationSaveRegressionTests
{
    [Fact]
    public async Task Editing_price_and_removing_a_row_from_loaded_revision_saves_cleanly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new InvoiceDbContext(new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var service = new QuotationService(db, new ReferenceNumberService(db), new QuotationCalculator(), new InvoiceCalculator());
        var original = await service.CreateDraftAsync(1);
        original.Items =
        [
            new QuotationItem { DisplayOrder = 1, DescriptionSnapshot = "Keep", UnitPrice = 100m, Quantity = 1 },
            new QuotationItem { DisplayOrder = 2, DescriptionSnapshot = "Remove", UnitPrice = 50m, Quantity = 1 }
        ];
        await service.SaveAsync(original);
        var revision = await service.CreateRevisionAsync(original.Id);
        var loaded = await service.GetAsync(revision.Id);
        Assert.NotNull(loaded);
        loaded.Items = loaded.Items.Where(item => item.DescriptionSnapshot != "Remove").ToList();
        loaded.Items[0].UnitPrice = 125m;

        await service.SaveAsync(loaded);
        db.ChangeTracker.Clear();

        var saved = await service.GetAsync(revision.Id);
        Assert.NotNull(saved);
        Assert.Single(saved.Items);
        Assert.Equal(125m, saved.Items[0].UnitPrice);
        Assert.Equal(125m, saved.GrandTotal);
    }

    [Fact]
    public async Task Editing_only_a_price_on_loaded_quotation_saves_cleanly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new InvoiceDbContext(new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var service = new QuotationService(db, new ReferenceNumberService(db), new QuotationCalculator(), new InvoiceCalculator());
        var quotation = await service.CreateDraftAsync(1);
        quotation.Items.Add(new QuotationItem { DescriptionSnapshot = "Edit", UnitPrice = 100m, Quantity = 1 });
        await service.SaveAsync(quotation);
        var loaded = await service.GetAsync(quotation.Id);
        Assert.NotNull(loaded);
        loaded.Items[0].UnitPrice = 75m;

        await service.SaveAsync(loaded);
        db.ChangeTracker.Clear();

        Assert.Equal(75m, (await service.GetAsync(quotation.Id))!.Items[0].UnitPrice);
    }

    [Fact]
    public async Task A_genuinely_missing_retained_row_still_reports_a_conflict()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new InvoiceDbContext(new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var service = new QuotationService(db, new ReferenceNumberService(db), new QuotationCalculator(), new InvoiceCalculator());
        var quotation = await service.CreateDraftAsync(1);
        quotation.Items.Add(new QuotationItem { DescriptionSnapshot = "Edit", UnitPrice = 100m, Quantity = 1 });
        await service.SaveAsync(quotation);
        var loaded = await service.GetAsync(quotation.Id);
        Assert.NotNull(loaded);
        loaded.Items[0].UnitPrice = 75m;
        await db.QuotationItems.Where(item => item.Id == loaded.Items[0].Id).ExecuteDeleteAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.SaveAsync(loaded));
    }
}
