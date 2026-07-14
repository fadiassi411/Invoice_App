using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;
using InvoiceSoftware.Data;
using InvoiceSoftware.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace InvoiceSoftware.Tests;

public sealed class InventoryIntegrationTests
{
    [Fact]
    public async Task Product_reference_is_unique_and_searchable()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var inventory = new InventoryService(fixture.Db);
        await inventory.SaveProductAsync(NewProduct("REF-100", 10), 10, "Opening stock");
        await Assert.ThrowsAsync<InvalidOperationException>(() => inventory.SaveProductAsync(NewProduct("REF-100", 1), 1, "Opening stock"));
        var results = await inventory.SearchProductsAsync("REF-100");
        Assert.Single(results);
    }

    [Fact]
    public async Task Finalized_invoice_deducts_once_and_modifications_apply_only_the_difference()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var inventory = new InventoryService(fixture.Db);
        var product = await inventory.SaveProductAsync(NewProduct("PART-1", 10), 10, "Opening stock");
        var service = fixture.InvoiceService();
        var invoice = InvoiceFor(product, 5);

        await service.SaveAsync(invoice);
        Assert.Equal(5m, await fixture.QuantityAsync(product.Id));
        Assert.Equal(product.Description, invoice.Items[0].Description);
        Assert.Equal(product.SellingPrice, invoice.Items[0].UnitPrice);

        await service.SaveAsync(invoice);
        Assert.Equal(5m, await fixture.QuantityAsync(product.Id));

        invoice.Items[0].Quantity = 7;
        await service.SaveAsync(invoice);
        Assert.Equal(3m, await fixture.QuantityAsync(product.Id));

        invoice.Items[0].Quantity = 4;
        await service.SaveAsync(invoice);
        Assert.Equal(6m, await fixture.QuantityAsync(product.Id));
        Assert.Equal(3, await fixture.Db.StockMovements.CountAsync(x => x.RelatedInvoiceId == invoice.Id));
    }

    [Fact]
    public async Task Deleting_finalized_invoice_restores_stock_and_records_reversal()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var product = await new InventoryService(fixture.Db).SaveProductAsync(NewProduct("PART-2", 8), 8, "Opening stock");
        var invoice = InvoiceFor(product, 3); var service = fixture.InvoiceService();
        await service.SaveAsync(invoice); await service.SoftDeleteAsync(invoice.Id);
        Assert.Equal(8m, await fixture.QuantityAsync(product.Id));
        Assert.True(await fixture.Db.StockMovements.AnyAsync(x => x.RelatedInvoiceId == invoice.Id && x.TransactionType == StockMovementType.InvoiceCancellation));
    }

    [Fact]
    public async Task Insufficient_stock_is_rejected_but_non_stock_services_are_not_deducted()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var inventory = new InventoryService(fixture.Db);
        var product = await inventory.SaveProductAsync(NewProduct("PART-3", 2), 2, "Opening stock");
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.InvoiceService().SaveAsync(InvoiceFor(product, 3)));
        Assert.Equal(2m, await fixture.QuantityAsync(product.Id));

        var nonStock = NewProduct("SERVICE-1", 0); nonStock.Type = ProductType.Service; nonStock.TrackStock = false;
        nonStock = await inventory.SaveProductAsync(nonStock, 0, "Service");
        await fixture.InvoiceService().SaveAsync(InvoiceFor(nonStock, 100));
        Assert.Equal(0m, await fixture.QuantityAsync(nonStock.Id));
    }

    [Fact]
    public async Task Invoice_keeps_historical_product_and_cost_snapshots()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var inventory = new InventoryService(fixture.Db);
        var product = await inventory.SaveProductAsync(NewProduct("PART-4", 4), 4, "Opening stock");
        var invoice = InvoiceFor(product, 1); await fixture.InvoiceService().SaveAsync(invoice);
        var originalDescription = invoice.Items[0].Description; var originalCost = invoice.Items[0].CostPriceSnapshot;
        product.Description = "Changed catalog description"; product.CostPrice = 999;
        await inventory.SaveProductAsync(product);
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.Invoices.Include(x => x.Items).SingleAsync(x => x.Id == invoice.Id);
        Assert.Equal(originalDescription, stored.Items[0].Description);
        Assert.Equal(originalCost, stored.Items[0].CostPriceSnapshot);
    }

    [Fact]
    public async Task Backup_contains_inventory_tables_and_data()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"invoice-backup-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(folder);
        var database = Path.Combine(folder, "source.db");
        var options = new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite($"Data Source={database}").Options;
        await using (var db = new InvoiceDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var product = await new InventoryService(db).SaveProductAsync(NewProduct("BACKUP-1", 12), 12, "Opening stock");
            var archivePath = Path.Combine(folder, "backup.zip");
            await new BackupService(db).BackupAsync(archivePath);
            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.GetEntry("invoice-software.db"); Assert.NotNull(entry);
            var restored = Path.Combine(folder, "restored.db"); entry!.ExtractToFile(restored);
            await using var restoredDb = new InvoiceDbContext(new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite($"Data Source={restored}").Options);
            Assert.True(await restoredDb.Products.AnyAsync(x => x.Code == "BACKUP-1"));
            Assert.True(await restoredDb.StockMovements.AnyAsync(x => x.ProductId == product.Id));
        }
        SqliteConnection.ClearAllPools();
        Directory.Delete(folder, true);
    }

    [Fact]
    public async Task Fully_paid_invoice_can_get_one_receipt_without_counting_payment_twice()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var invoice = new Invoice
        {
            ReferenceNumber = "INV-PAID-RECEIPT", InvoiceNumber = "INV-PAID-RECEIPT", CustomerId = 1,
            Status = InvoiceStatus.Finalized,
            Items = [new InvoiceItem { Description = "Service", Quantity = 1, UnitPrice = 150m, SortOrder = 1 }]
        };
        await fixture.InvoiceService().SaveAsync(invoice);
        await fixture.InvoiceService().MarkPaidAsync(invoice.Id);

        var receiptService = fixture.ReceiptService();
        var receipt = await receiptService.CreateForRecordedPaymentAsync(invoice.Id, PaymentMethodType.Cash, "PAID", "Recorded payment receipt");
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.Invoices.Include(x => x.Receipts).SingleAsync(x => x.Id == invoice.Id);

        Assert.Equal(150m, receipt.PaymentAmount);
        Assert.Equal(150m, stored.AmountPaid);
        Assert.Equal(0m, stored.RemainingBalance);
        Assert.Single(stored.Receipts);
        await Assert.ThrowsAsync<InvalidOperationException>(() => receiptService.CreateForRecordedPaymentAsync(invoice.Id, PaymentMethodType.Cash, null));
    }

    private static Product NewProduct(string code, decimal quantity) => new()
    {
        Code = code, Name = "Test Product", Description = "Test product description", Unit = "Piece",
        CostPrice = 4m, SellingPrice = 10m, CurrentQuantity = quantity, MinimumQuantity = 1,
        TrackStock = true, IsActive = true
    };
    private static Invoice InvoiceFor(Product product, decimal quantity) => new()
    {
        ReferenceNumber = $"INV-{Guid.NewGuid():N}", InvoiceNumber = "TEST", CustomerId = 1,
        Status = InvoiceStatus.Finalized,
        Items = [new InvoiceItem { ProductId = product.Id, Description = product.Description, Unit = product.Unit, Quantity = quantity, UnitPrice = product.SellingPrice, TaxPercentage = product.TaxPercentage, SortOrder = 1 }]
    };

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public InvoiceDbContext Db { get; }
        private TestDatabase(SqliteConnection connection, InvoiceDbContext db) { _connection = connection; Db = db; }
        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new InvoiceDbContext(new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); return new TestDatabase(connection, db);
        }
        public InvoiceService InvoiceService() => new(Db, new ReferenceNumberService(Db), new InvoiceCalculator());
        public ReceiptService ReceiptService() => new(Db, new ReferenceNumberService(Db), new InvoiceCalculator());
        public async Task<decimal> QuantityAsync(int productId)
        {
            Db.ChangeTracker.Clear(); return await Db.Products.Where(x => x.Id == productId).Select(x => x.CurrentQuantity).SingleAsync();
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }
}
