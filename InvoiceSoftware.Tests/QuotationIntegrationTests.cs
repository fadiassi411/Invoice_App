using System.IO.Compression;
using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;
using InvoiceSoftware.Data;
using InvoiceSoftware.Reporting;
using InvoiceSoftware.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InvoiceSoftware.Tests;

public sealed class QuotationIntegrationTests
{
    [Fact]
    public async Task Stock_and_manual_lines_save_snapshots_without_deducting_stock()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProductAsync("Q-PART-1", 12m);
        var quotation = await fixture.Service.CreateDraftAsync(1);
        quotation.Items.Add(StockLine(product, 5));
        quotation.Items.Add(new QuotationItem { DisplayOrder = 2, LineNumber = 2, ItemType = QuotationItemType.ManualItem, DescriptionSnapshot = "Installation service", UnitSnapshot = "job", Quantity = 1, UnitPrice = 80m, TaxPercentage = 5m });

        await fixture.Service.SaveAsync(quotation);
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.Quotations.Include(x => x.Items).SingleAsync(x => x.Id == quotation.Id);

        Assert.Equal(12m, await fixture.QuantityAsync(product.Id));
        Assert.Equal(2, stored.Items.Count);
        Assert.Equal(product.Description, stored.Items.Single(x => x.StockItemId == product.Id).DescriptionSnapshot);
        Assert.Equal(product.Code, stored.Items.Single(x => x.StockItemId == product.Id).ItemReferenceSnapshot);
    }

    [Fact]
    public void Calculator_supports_line_and_overall_discounts_multiple_taxes_and_inclusive_tax()
    {
        var quotation = new Quotation
        {
            CurrencyCode = "USD", TaxMode = TaxMode.Exclusive, OverallDiscountType = DiscountType.FixedAmount, OverallDiscountValue = 10m,
            Items =
            [
                new QuotationItem { LineNumber = 1, DisplayOrder = 1, DescriptionSnapshot = "A", Quantity = 2, UnitPrice = 100, DiscountType = DiscountType.Percentage, DiscountValue = 10, TaxPercentage = 5 },
                new QuotationItem { LineNumber = 2, DisplayOrder = 2, DescriptionSnapshot = "B", Quantity = 1, UnitPrice = 50, DiscountType = DiscountType.FixedAmount, DiscountValue = 5, TaxPercentage = 10 }
            ]
        };
        var calculator = new QuotationCalculator();
        var totals = calculator.Calculate(quotation);
        Assert.Equal(250m, totals.Subtotal);
        Assert.Equal(35m, totals.Discount);
        Assert.Equal(215m, totals.NetBeforeTax);
        Assert.Equal(13.5m, totals.Tax);
        Assert.Equal(228.5m, totals.GrandTotal);

        quotation.TaxMode = TaxMode.Inclusive; quotation.OverallDiscountValue = 0;
        totals = calculator.Calculate(quotation);
        Assert.True(totals.Tax > 0);
        Assert.Equal(225m, totals.GrandTotal);
    }

    [Fact]
    public async Task Quotation_numbers_are_unique_sequential_and_abandoned_numbers_are_not_reused()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.Service.CreateDraftAsync(1);
        var second = await fixture.Service.CreateDraftAsync(1);
        var third = await fixture.Service.CreateDraftAsync(1);
        Assert.EndsWith("000001", first.QuotationNumber);
        Assert.EndsWith("000002", second.QuotationNumber);
        Assert.EndsWith("000003", third.QuotationNumber);
        Assert.Equal(3, new[] { first.QuotationNumber, second.QuotationNumber, third.QuotationNumber }.Distinct().Count());
    }

    [Fact]
    public async Task Draft_can_be_reopened_modified_and_revision_preserves_previous_version()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProductAsync("Q-PART-2", 4);
        var quotation = await fixture.Service.CreateDraftAsync(1); quotation.Subject = "Original"; quotation.Items.Add(StockLine(product, 1));
        await fixture.Service.SaveAsync(quotation);
        fixture.Db.ChangeTracker.Clear();
        var reopened = await fixture.Service.GetAsync(quotation.Id); Assert.NotNull(reopened);
        reopened!.Subject = "Modified"; await fixture.Service.SaveAsync(reopened);
        fixture.Db.ChangeTracker.Clear();
        var revision = await fixture.Service.CreateRevisionAsync(quotation.Id);
        Assert.Equal(quotation.QuotationNumber, revision.QuotationNumber);
        Assert.Equal(1, revision.RevisionNumber);
        Assert.Equal(quotation.Id, revision.ParentQuotationId);
        Assert.Equal(2, await fixture.Db.Quotations.CountAsync(x => x.QuotationNumber == quotation.QuotationNumber));
        Assert.Equal("Modified", (await fixture.Service.GetAsync(quotation.Id))!.Subject);
    }

    [Fact]
    public async Task Accepted_quotation_converts_once_with_identical_total_and_no_stock_change()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProductAsync("Q-PART-3", 9);
        var quotation = await fixture.Service.CreateDraftAsync(1);
        quotation.OverallDiscountType = DiscountType.FixedAmount; quotation.OverallDiscountValue = 7m; quotation.ShippingCharge = 4m;
        quotation.Items.Add(StockLine(product, 2));
        await fixture.Service.SaveAsync(quotation);
        await fixture.Service.ChangeStatusAsync(quotation.Id, QuotationStatus.Sent);
        await fixture.Service.ChangeStatusAsync(quotation.Id, QuotationStatus.Accepted);

        var invoice = await fixture.Service.ConvertToInvoiceAsync(quotation.Id);
        Assert.Equal(quotation.GrandTotal, invoice.GrandTotal);
        Assert.Equal(9m, await fixture.QuantityAsync(product.Id));
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(quotation.Id, invoice.SourceQuotationId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ConvertToInvoiceAsync(quotation.Id));
    }

    [Fact]
    public async Task Search_filters_and_expiry_status_are_applied()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProductAsync("Q-PART-4", 3);
        var quotation = await fixture.Service.CreateDraftAsync(1);
        quotation.QuotationDate = DateTime.Today.AddDays(-10); quotation.ValidUntil = DateTime.Today.AddDays(-1); quotation.Subject = "Expired project";
        quotation.Items.Add(StockLine(product, 1)); await fixture.Service.SaveAsync(quotation);

        var results = await fixture.Service.SearchAsync(new QuotationSearchCriteria("Expired project", Status: QuotationStatus.Expired, Validity: QuotationValidityFilter.Expired));
        Assert.Single(results);
        Assert.Equal(QuotationStatus.Expired, results[0].Status);
    }

    [Fact]
    public async Task Duplicate_gets_a_new_number_and_history_is_audited()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProductAsync("Q-PART-5", 3);
        var quotation = await fixture.Service.CreateDraftAsync(1); quotation.Items.Add(StockLine(product, 1)); await fixture.Service.SaveAsync(quotation);
        var copy = await fixture.Service.DuplicateAsync(quotation.Id);
        Assert.NotEqual(quotation.QuotationNumber, copy.QuotationNumber);
        Assert.Equal(0, copy.RevisionNumber);
        Assert.True(await fixture.Db.AuditLogs.AnyAsync(x => x.EntityName == nameof(Quotation) && x.Action == "Create"));
    }

    [Fact]
    public async Task One_page_and_multi_page_quotation_pdfs_are_generated_from_the_same_template()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"quotation-pdf-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(folder);
        try
        {
            var service = new QuotationPdfService(); var calculator = new QuotationCalculator(); var company = new CompanySettings { CompanyName = "Test Company", Currency = "USD" };
            var onePage = QuoteForPdf(2); calculator.Calculate(onePage); var onePath = Path.Combine(folder, "one.pdf"); service.ExportQuotation(onePage, company, onePath);
            var manyPages = QuoteForPdf(75); calculator.Calculate(manyPages); var manyPath = Path.Combine(folder, "many.pdf"); service.ExportQuotation(manyPages, company, manyPath);
            Assert.True(new FileInfo(onePath).Length > 1_000);
            Assert.True(new FileInfo(manyPath).Length > new FileInfo(onePath).Length);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(await File.ReadAllBytesAsync(onePath), 0, 4));
            var qaFolder = Environment.GetEnvironmentVariable("QUOTATION_QA_FOLDER");
            if (!string.IsNullOrWhiteSpace(qaFolder))
            {
                Directory.CreateDirectory(qaFolder);
                File.Copy(onePath, Path.Combine(qaFolder, "quotation-one-page.pdf"), true);
                File.Copy(manyPath, Path.Combine(qaFolder, "quotation-multi-page.pdf"), true);
            }
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task Backup_and_restore_payload_contains_quotation_records()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"quotation-backup-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(folder);
        var database = Path.Combine(folder, "source.db");
        try
        {
            var options = new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite($"Data Source={database}").Options;
            await using var db = new InvoiceDbContext(options); await db.Database.EnsureCreatedAsync();
            var service = NewService(db); var quotation = await service.CreateDraftAsync(1);
            quotation.Items.Add(new QuotationItem { LineNumber = 1, DisplayOrder = 1, ItemType = QuotationItemType.ManualItem, DescriptionSnapshot = "Backup quotation line", Quantity = 1, UnitPrice = 25 });
            await service.SaveAsync(quotation);
            var archivePath = Path.Combine(folder, "backup.zip"); await new BackupService(db).BackupAsync(archivePath);
            using var archive = ZipFile.OpenRead(archivePath); var entry = archive.GetEntry("invoice-software.db"); Assert.NotNull(entry);
            var restored = Path.Combine(folder, "restored.db"); entry!.ExtractToFile(restored);
            await using var restoredDb = new InvoiceDbContext(new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite($"Data Source={restored}").Options);
            Assert.True(await restoredDb.Quotations.AnyAsync(x => x.QuotationNumber == quotation.QuotationNumber));
            Assert.True(await restoredDb.QuotationItems.AnyAsync(x => x.DescriptionSnapshot == "Backup quotation line"));
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task Migration_upgrades_an_existing_database_without_losing_customer_data()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"quotation-migration-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(folder);
        var database = Path.Combine(folder, "upgrade.db");
        try
        {
            var options = new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite($"Data Source={database}").Options;
            await using (var oldDb = new InvoiceDbContext(options))
            {
                await oldDb.GetService<IMigrator>().MigrateAsync("20260713144306_BackfillLegacyInventoryData");
                await oldDb.Database.ExecuteSqlRawAsync("INSERT INTO Customers (CustomerCode, Name, CreditLimit, OpeningBalance, IsActive, CreatedAt, IsDeleted) VALUES ('CUS-PRESERVE', 'Preserved Customer', 0, 0, 1, CURRENT_TIMESTAMP, 0)");
                await oldDb.GetService<IMigrator>().MigrateAsync();
            }
            await using var upgraded = new InvoiceDbContext(options);
            Assert.True(await upgraded.Customers.AnyAsync(x => x.CustomerCode == "CUS-PRESERVE" && x.Name == "Preserved Customer"));
            Assert.Equal("QUO", (await upgraded.CompanySettings.SingleAsync()).QuotationPrefix);
            Assert.True(await upgraded.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='Quotations'").SingleAsync() == 1);
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }
    }

    private static Quotation QuoteForPdf(int count) => new()
    {
        QuotationNumber = "QUO-2026-000001", RevisionNumber = 0, CustomerNameSnapshot = "Example Customer", CurrencyCode = "USD",
        ProjectName = "Control System Upgrade", Subject = "Equipment and engineering quotation", PaymentTerms = "30 days", DeliveryPeriod = "4 weeks", Warranty = "12 months",
        Items = Enumerable.Range(1, count).Select(i => new QuotationItem { LineNumber = i, DisplayOrder = i, DescriptionSnapshot = $"Professional multiline quotation item {i} with technical details and specifications", ItemReferenceSnapshot = $"PART-{i:000}", Quantity = 1 + i % 3, UnitSnapshot = "ea", UnitPrice = 10 + i, TaxPercentage = i % 2 == 0 ? 5 : 10 }).ToList()
    };

    private static QuotationItem StockLine(Product product, decimal quantity) => new()
    {
        LineNumber = 1, DisplayOrder = 1, ItemType = QuotationItemType.StockItem, StockItemId = product.Id,
        ItemReferenceSnapshot = product.Code, DescriptionSnapshot = product.Description, UnitSnapshot = product.Unit,
        AvailableStockSnapshot = product.CurrentQuantity, Quantity = quantity, UnitPrice = product.SellingPrice, TaxPercentage = product.TaxPercentage
    };

    private static QuotationService NewService(InvoiceDbContext db) => new(db, new ReferenceNumberService(db), new QuotationCalculator(), new InvoiceCalculator());

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public InvoiceDbContext Db { get; }
        public QuotationService Service { get; }
        private Fixture(SqliteConnection connection, InvoiceDbContext db) { _connection = connection; Db = db; Service = NewService(db); }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new InvoiceDbContext(new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite(connection).Options); await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }
        public async Task<Product> AddProductAsync(string code, decimal quantity)
            => await new InventoryService(Db).SaveProductAsync(new Product { Code = code, PartNumber = code, Name = code, Description = $"Description {code}", Unit = "ea", CostPrice = 5, SellingPrice = 20, TaxPercentage = 5, TrackStock = true, IsActive = true }, quantity, "Opening stock");
        public async Task<decimal> QuantityAsync(int productId) { Db.ChangeTracker.Clear(); return await Db.Products.Where(x => x.Id == productId).Select(x => x.CurrentQuantity).SingleAsync(); }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }
}
