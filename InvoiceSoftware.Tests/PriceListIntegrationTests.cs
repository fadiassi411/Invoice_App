using ClosedXML.Excel;
using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using InvoiceSoftware.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Tests;

public sealed class PriceListIntegrationTests
{
    [Fact]
    public async Task Price_list_items_are_stored_separately_from_inventory_products()
    {
        await using var fixture = await PriceListFixture.CreateAsync();
        var inventory = new InventoryService(fixture.Db);
        var initialInventoryCount = await fixture.Db.Products.CountAsync();
        await inventory.SaveProductAsync(new Product
        {
            Code = "STORE-001", Name = "Stock item", Description = "Inventory only",
            Unit = "Piece", TrackStock = true, IsActive = true
        }, 5, "Opening stock");

        var saved = await fixture.Service.SaveAsync(new PriceListItem
        {
            ReferenceNumber = "PRICE-001", ProductName = "Catalogue item",
            Description = "Price list only", Unit = "Piece", SellingPrice = 125m
        });

        Assert.True(saved.Id > 0);
        Assert.Equal(initialInventoryCount + 1, await fixture.Db.Products.CountAsync());
        Assert.Single(await fixture.Db.PriceListItems.ToListAsync());
        Assert.Empty(await inventory.SearchProductsAsync("PRICE-001"));
        Assert.Single(await fixture.Service.SearchAsync("Catalogue"));
    }

    [Fact]
    public async Task Excel_round_trip_preserves_fields_and_embedded_image_without_touching_inventory()
    {
        await using var fixture = await PriceListFixture.CreateAsync();
        var initialInventoryCount = await fixture.Db.Products.CountAsync();
        var sourceImage = Path.Combine(fixture.Folder, "source.png");
        await File.WriteAllBytesAsync(sourceImage, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2pWQAAAAASUVORK5CYII="));
        var original = await fixture.Service.SaveAsync(new PriceListItem
        {
            ReferenceNumber = "PL-100", ProductName = "Temperature sensor",
            Description = "Stainless steel probe", Category = "Sensors", Brand = "MicroBrain",
            Unit = "Piece", SellingPrice = 45.75m, TaxRate = 11m, ImagePath = sourceImage
        });
        var workbookPath = Path.Combine(fixture.Folder, "price-list.xlsx");

        await fixture.Service.ExportExcelAsync(workbookPath, [original]);
        using (var workbook = new XLWorkbook(workbookPath))
        {
            var sheet = workbook.Worksheet("Price List");
            Assert.Equal("PL-100", sheet.Cell(2, 2).GetString());
            Assert.Equal(45.75m, sheet.Cell(2, 8).GetValue<decimal>());
            Assert.Single(sheet.Pictures);
            Assert.True(sheet.Column(12).IsHidden);

            sheet.Cell(2, 2).Value = "PL-200";
            sheet.Cell(2, 3).Value = "Imported sensor";
            sheet.Cell(2, 8).Value = 52.50m;
            sheet.Cell(2, 12).Clear(); // Force the portable embedded image path.
            workbook.Save();
        }

        var importedCount = await fixture.Service.ImportExcelAsync(workbookPath);
        fixture.Db.ChangeTracker.Clear();
        var imported = await fixture.Db.PriceListItems.SingleAsync(x => x.ReferenceNumber == "PL-200");

        Assert.Equal(1, importedCount);
        Assert.Equal("Imported sensor", imported.ProductName);
        Assert.Equal(52.50m, imported.SellingPrice);
        Assert.False(string.IsNullOrWhiteSpace(imported.ImagePath));
        Assert.True(File.Exists(imported.ImagePath));
        Assert.Equal(initialInventoryCount, await fixture.Db.Products.CountAsync());
    }

    [Fact]
    public async Task Import_rejects_duplicate_references_before_saving_rows()
    {
        await using var fixture = await PriceListFixture.CreateAsync();
        var workbookPath = Path.Combine(fixture.Folder, "duplicates.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Price List");
            sheet.Cell(1, 1).Value = "ReferenceNumber";
            sheet.Cell(1, 2).Value = "ProductName";
            sheet.Cell(1, 3).Value = "SellingPrice";
            sheet.Cell(2, 1).Value = "DUP-1";
            sheet.Cell(2, 2).Value = "First";
            sheet.Cell(2, 3).Value = 10m;
            sheet.Cell(3, 1).Value = "DUP-1";
            sheet.Cell(3, 2).Value = "Second";
            sheet.Cell(3, 3).Value = 20m;
            workbook.SaveAs(workbookPath);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ImportExcelAsync(workbookPath));
        Assert.DoesNotContain(fixture.Db.ChangeTracker.Entries(), x => x.State is EntityState.Added or EntityState.Modified);
        Assert.Empty(await fixture.Db.PriceListItems.ToListAsync());
    }

    [Fact]
    public async Task Deleting_one_item_does_not_remove_other_price_list_or_inventory_items()
    {
        await using var fixture = await PriceListFixture.CreateAsync();
        var originalInventoryCount = await fixture.Db.Products.CountAsync();
        var first = await fixture.Service.SaveAsync(new PriceListItem
        {
            ReferenceNumber = "DELETE-ONE", ProductName = "First", SellingPrice = 10m
        });
        await fixture.Service.SaveAsync(new PriceListItem
        {
            ReferenceNumber = "KEEP-ONE", ProductName = "Second", SellingPrice = 20m
        });

        await fixture.Service.DeleteAsync(first.Id);

        Assert.Equal(["KEEP-ONE"], (await fixture.Service.SearchAsync()).Select(x => x.ReferenceNumber).ToArray());
        Assert.True((await fixture.Db.PriceListItems.IgnoreQueryFilters().SingleAsync(x => x.Id == first.Id)).IsDeleted);
        Assert.Equal(originalInventoryCount, await fixture.Db.Products.CountAsync());
    }

    [Fact]
    public async Task Deleting_a_supplier_list_removes_only_that_suppliers_price_list_entries()
    {
        await using var fixture = await PriceListFixture.CreateAsync();
        var originalInventoryCount = await fixture.Db.Products.CountAsync();
        var supplier = new Supplier { CompanyName = "Supplier A", IsActive = true };
        var otherSupplier = new Supplier { CompanyName = "Supplier B", IsActive = true };
        fixture.Db.Suppliers.AddRange(supplier, otherSupplier);
        await fixture.Db.SaveChangesAsync();
        foreach (var index in Enumerable.Range(1, 120))
        {
            fixture.Db.PriceListItems.Add(new PriceListItem
            {
                ReferenceNumber = $"A-{index:000}", ProductName = $"A item {index}",
                SellingPrice = index, SupplierId = supplier.Id
            });
        }
        fixture.Db.PriceListItems.AddRange(
            new PriceListItem { ReferenceNumber = "B-1", ProductName = "B item", SellingPrice = 5m, SupplierId = otherSupplier.Id },
            new PriceListItem { ReferenceNumber = "MANUAL-1", ProductName = "Manual item", SellingPrice = 6m });
        await fixture.Db.SaveChangesAsync();

        Assert.Equal(120, await fixture.Service.CountSupplierItemsAsync(supplier.Id));
        Assert.Equal(120, await fixture.Service.DeleteSupplierItemsAsync(supplier.Id));
        Assert.Equal(0, await fixture.Service.CountSupplierItemsAsync(supplier.Id));
        Assert.Equal(0, await fixture.Service.DeleteSupplierItemsAsync(supplier.Id));
        Assert.Equal(["B-1", "MANUAL-1"], (await fixture.Service.SearchAsync()).Select(x => x.ReferenceNumber).ToArray());
        Assert.Equal(122, await fixture.Db.PriceListItems.IgnoreQueryFilters().CountAsync());
        Assert.Equal(originalInventoryCount, await fixture.Db.Products.CountAsync());
    }

    private sealed class PriceListFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public string Folder { get; }
        public InvoiceDbContext Db { get; }
        public PriceListService Service { get; }

        private PriceListFixture(string folder, SqliteConnection connection, InvoiceDbContext db)
        {
            Folder = folder;
            this.connection = connection;
            Db = db;
            Service = new PriceListService(db, Path.Combine(folder, "images"));
        }

        public static async Task<PriceListFixture> CreateAsync()
        {
            var folder = Path.Combine(Path.GetTempPath(), $"invoice-price-list-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new InvoiceDbContext(new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new PriceListFixture(folder, connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
            if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
        }
    }
}
