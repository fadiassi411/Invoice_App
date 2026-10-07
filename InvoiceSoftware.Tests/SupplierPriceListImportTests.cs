using ClosedXML.Excel;
using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using InvoiceSoftware.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace InvoiceSoftware.Tests;

public sealed class SupplierPriceListImportTests
{
    [Fact]
    public async Task Detects_headers_below_titles_and_reordered_columns_then_previews_without_writes()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var file = fixture.PathFor("title-and-reordered.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Supplier List");
            sheet.Range("A1:D1").Merge().Value = "ACME 2026 PRICE LIST";
            sheet.Cell(3, 1).Value = "Currency";
            sheet.Cell(3, 2).Value = "Unit Price";
            sheet.Cell(3, 3).Value = "Description";
            sheet.Cell(3, 4).Value = "SKU";
            sheet.Cell(4, 1).Value = "USD";
            sheet.Cell(4, 2).Value = 12.3456m;
            sheet.Cell(4, 3).Value = "Precision sensor";
            sheet.Cell(4, 4).Value = "A-001";
            workbook.SaveAs(file);
        }

        var analysis = await fixture.Service.AnalyzeAsync(file, fixture.Supplier.Id);
        var candidate = Assert.Single(analysis.Candidates, x => x.SheetName == "Supplier List" && x.HeaderRowNumber == 3);
        Assert.Equal(3, candidate.HeaderRowNumber);
        var preview = await fixture.Service.BuildPreviewAsync(file, fixture.Supplier.Id, Mapping(candidate, ExistingItemMode.CreateOnly));

        var record = Assert.Single(preview.Records);
        Assert.Equal("A-001", record.ReferenceNumber);
        Assert.Equal("Precision sensor", record.ProductName);
        Assert.Equal(12.3456m, record.Price);
        Assert.Equal("USD", record.Currency);
        Assert.Equal(ImportRecordAction.Create, record.Action);
        Assert.Empty(await fixture.Db.PriceListItems.ToListAsync());
        Assert.Empty(await fixture.Db.SupplierPriceListMappings.ToListAsync());
    }

    [Fact]
    public async Task Multiple_price_columns_require_an_explicit_price_mapping()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var file = fixture.PathFor("multiple-prices.xlsx");
        CreateWorkbook(file, ["Item Code", "Description", "Retail Price", "Wholesale Price"],
            [["P-1", "Controller", 120m, 90m]]);

        var analysis = await fixture.Service.AnalyzeAsync(file, fixture.Supplier.Id);
        var candidate = analysis.Candidates.First();

        Assert.True(candidate.RequiresConfirmation);
        Assert.False(candidate.SuggestedMappings.ContainsKey(PriceListImportField.Price));
        Assert.Contains(candidate.Columns, x => x.Header == "Retail Price");
        Assert.Contains(candidate.Columns, x => x.Header == "Wholesale Price");
    }

    [Fact]
    public async Task Preserves_formatted_leading_zero_item_codes()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var file = fixture.PathFor("leading-zero.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Items");
            sheet.Cell(1, 1).Value = "Part Number";
            sheet.Cell(1, 2).Value = "Product Name";
            sheet.Cell(1, 3).Value = "Price";
            sheet.Cell(2, 1).Value = 123;
            sheet.Cell(2, 1).Style.NumberFormat.Format = "000000";
            sheet.Cell(2, 2).Value = "Relay";
            sheet.Cell(2, 3).Value = 5.25m;
            workbook.SaveAs(file);
        }

        var analysis = await fixture.Service.AnalyzeAsync(file, fixture.Supplier.Id);
        var preview = await fixture.Service.BuildPreviewAsync(file, fixture.Supplier.Id, Mapping(analysis.Candidates.First()));

        Assert.Equal("000123", Assert.Single(preview.Records).ReferenceNumber);
    }

    [Fact]
    public async Task Duplicate_and_invalid_prices_are_reported_with_sheet_row_and_original_value()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var file = fixture.PathFor("issues.xlsx");
        CreateWorkbook(file, ["Reference", "Description", "Unit Price"],
            [["DUP-1", "First", "not-a-price"], ["DUP-1", "Second", 20m]]);

        var analysis = await fixture.Service.AnalyzeAsync(file, fixture.Supplier.Id);
        var preview = await fixture.Service.BuildPreviewAsync(file, fixture.Supplier.Id, Mapping(analysis.Candidates.First()));

        Assert.True(preview.ErrorCount >= 3);
        Assert.Contains(preview.Issues, x => x.ExcelRowNumber == 2 && x.OriginalValue == "not-a-price" && x.Message.Contains("valid price"));
        Assert.Equal(2, preview.Issues.Count(x => x.Message.Contains("appears more than once")));
        Assert.All(preview.Issues, x => Assert.False(string.IsNullOrWhiteSpace(x.SheetName)));
        Assert.Empty(await fixture.Db.PriceListItems.ToListAsync());
    }

    [Fact]
    public async Task Ambiguous_single_separator_price_requires_an_explicit_parsing_choice()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var file = fixture.PathFor("ambiguous-price.xlsx");
        CreateWorkbook(file, ["Item Number", "Description", "Price"], [["AMB-1", "Ambiguous", "1,234"]]);
        var analysis = await fixture.Service.AnalyzeAsync(file, fixture.Supplier.Id);

        var automatic = await fixture.Service.BuildPreviewAsync(file, fixture.Supplier.Id, Mapping(analysis.Candidates.First()));
        Assert.Contains(automatic.Issues, x => x.Message.Contains("ambiguous", StringComparison.OrdinalIgnoreCase));

        var automaticMapping = Mapping(analysis.Candidates.First());
        var explicitMapping = new PriceListImportMapping
        {
            SheetName = automaticMapping.SheetName,
            HeaderRowNumber = automaticMapping.HeaderRowNumber,
            Columns = automaticMapping.Columns,
            ExistingItems = automaticMapping.ExistingItems,
            DecimalSeparator = DecimalSeparatorMode.Comma
        };
        var explicitPreview = await fixture.Service.BuildPreviewAsync(file, fixture.Supplier.Id, explicitMapping);
        Assert.Equal(1.234m, Assert.Single(explicitPreview.Records).Price);
    }

    [Fact]
    public async Task Importing_valid_records_only_requires_the_explicit_flag_and_reports_skips()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var file = fixture.PathFor("valid-only.xlsx");
        CreateWorkbook(file, ["Reference", "Description", "Price"],
            [["GOOD-1", "Good", 10m], ["BAD-1", "Bad", "invalid"]]);
        var analysis = await fixture.Service.AnalyzeAsync(file, fixture.Supplier.Id);
        var preview = await fixture.Service.BuildPreviewAsync(file, fixture.Supplier.Id, Mapping(analysis.Candidates.First()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ImportAsync(preview, false));
        Assert.Empty(await fixture.Db.PriceListItems.ToListAsync());

        var result = await fixture.Service.ImportAsync(preview, true);
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Skipped);
        Assert.Equal("GOOD-1", Assert.Single(await fixture.Db.PriceListItems.ToListAsync()).ReferenceNumber);
    }

    [Fact]
    public async Task Existing_references_require_an_explicit_update_or_skip_choice()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        fixture.Db.PriceListItems.Add(new PriceListItem
        {
            ReferenceNumber = "EX-1", ProductName = "Old", Description = "Old", SellingPrice = 1m
        });
        await fixture.Db.SaveChangesAsync();
        var file = fixture.PathFor("existing.xlsx");
        CreateWorkbook(file, ["Reference", "Description", "Price"], [["EX-1", "New", 2m]]);
        var analysis = await fixture.Service.AnalyzeAsync(file, fixture.Supplier.Id);

        var blocked = await fixture.Service.BuildPreviewAsync(file, fixture.Supplier.Id, Mapping(analysis.Candidates.First(), ExistingItemMode.CreateOnly));
        Assert.True(Assert.Single(blocked.Records).HasErrors);

        var update = await fixture.Service.BuildPreviewAsync(file, fixture.Supplier.Id, Mapping(analysis.Candidates.First(), ExistingItemMode.UpdateMatching));
        Assert.Equal(ImportRecordAction.Update, Assert.Single(update.Records).Action);
    }

    [Fact]
    public async Task Confirmed_mapping_is_reused_by_header_name_and_layout_changes_are_rejected()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var firstFile = fixture.PathFor("first.xlsx");
        CreateWorkbook(firstFile, ["SKU", "Description", "Price", "Currency"], [["S-1", "Sensor", 9m, "USD"]]);
        var firstAnalysis = await fixture.Service.AnalyzeAsync(firstFile, fixture.Supplier.Id);
        var firstPreview = await fixture.Service.BuildPreviewAsync(firstFile, fixture.Supplier.Id, Mapping(firstAnalysis.Candidates.First()));
        await fixture.Service.ImportAsync(firstPreview, false);

        var reordered = fixture.PathFor("reordered.xlsx");
        CreateWorkbook(reordered, ["Currency", "Price", "Description", "SKU"], [["EUR", 10m, "Sensor 2", "S-2"]]);
        var reused = await fixture.Service.AnalyzeAsync(reordered, fixture.Supplier.Id);
        Assert.True(reused.SavedMappingApplied);
        Assert.NotNull(reused.SavedMapping);
        var candidate = reused.Candidates.First(x => x.SheetName == reused.SavedMapping!.SheetName);
        Assert.Equal(candidate.Columns.Single(x => x.Header == "SKU").ColumnIndex, reused.SavedMapping.Columns[PriceListImportField.ItemNumber]);

        var changed = fixture.PathFor("changed.xlsx");
        CreateWorkbook(changed, ["SKU", "Description"], [["S-3", "No price"]]);
        var changedAnalysis = await fixture.Service.AnalyzeAsync(changed, fixture.Supplier.Id);
        Assert.False(changedAnalysis.SavedMappingApplied);
        Assert.Contains("changed", changedAnalysis.SavedMappingMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Import_valid_only_is_explicit_and_failed_import_rolls_back_all_new_records()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var file = fixture.PathFor("rollback.xlsx");
        CreateWorkbook(file, ["Reference", "Description", "Price"],
            [["ROLL-A", "First", 10m], ["ROLL-B", "Second", 20m]]);
        var analysis = await fixture.Service.AnalyzeAsync(file, fixture.Supplier.Id);
        var preview = await fixture.Service.BuildPreviewAsync(file, fixture.Supplier.Id, Mapping(analysis.Candidates.First()));

        fixture.Db.PriceListItems.Add(new PriceListItem
        {
            ReferenceNumber = "ROLL-B", ProductName = "Concurrent", Description = "Concurrent", SellingPrice = 99m
        });
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => fixture.Service.ImportAsync(preview, false));
        Assert.False(await fixture.Db.PriceListItems.AnyAsync(x => x.ReferenceNumber == "ROLL-A"));
        Assert.True(await fixture.Db.PriceListItems.AnyAsync(x => x.ReferenceNumber == "ROLL-B"));
        Assert.Empty(await fixture.Db.SupplierPriceListMappings.ToListAsync());
    }

    [Fact]
    public async Task Formula_without_a_saved_value_is_flagged_before_import()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var file = fixture.PathFor("formula.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Prices");
            sheet.Cell(1, 1).Value = "Reference";
            sheet.Cell(1, 2).Value = "Description";
            sheet.Cell(1, 3).Value = "Price";
            sheet.Cell(2, 1).Value = "F-1";
            sheet.Cell(2, 2).Value = "Formula price";
            sheet.Cell(2, 3).FormulaA1 = "MISSING_NAME";
            workbook.SaveAs(file, false, false);
        }

        var analysis = await fixture.Service.AnalyzeAsync(file, fixture.Supplier.Id);
        var preview = await fixture.Service.BuildPreviewAsync(file, fixture.Supplier.Id, Mapping(analysis.Candidates.First()));
        Assert.Contains(preview.Issues, x => x.ExcelRowNumber == 2 && (x.Field == "Formula" || x.Field == "Price"));
        Assert.Empty(await fixture.Db.PriceListItems.ToListAsync());
    }

    [Fact]
    public async Task Large_import_revalidates_in_bulk_instead_of_querying_each_record()
    {
        var counter = new SelectCommandCounter();
        await using var fixture = await ImportFixture.CreateAsync(counter);
        var file = fixture.PathFor("large-list.xlsx");
        var rows = Enumerable.Range(1, 500)
            .Select(index => new object?[] { $"ITEM-{index:000000}", $"Item {index}", index + 0.125m })
            .ToArray();
        CreateWorkbook(file, ["Reference", "Description", "Price"], rows);
        var analysis = await fixture.Service.AnalyzeAsync(file, fixture.Supplier.Id);
        var preview = await fixture.Service.BuildPreviewAsync(file, fixture.Supplier.Id, Mapping(analysis.Candidates.First()));

        counter.Reset();
        var result = await fixture.Service.ImportAsync(preview, false);

        Assert.Equal(500, result.Created);
        Assert.Equal(500, await fixture.Db.PriceListItems.CountAsync());
        Assert.True(counter.SelectCount <= 4, $"Expected at most 4 SELECT commands during import, but observed {counter.SelectCount}.");
    }

    private static PriceListImportMapping Mapping(PriceListTableCandidate candidate, ExistingItemMode existing = ExistingItemMode.CreateOnly)
    {
        var columns = candidate.SuggestedMappings.ToDictionary(x => x.Key, x => x.Value);
        if (!columns.ContainsKey(PriceListImportField.Price))
            columns[PriceListImportField.Price] = candidate.Columns.First(x => x.Header.Contains("Price", StringComparison.OrdinalIgnoreCase)).ColumnIndex;
        return new PriceListImportMapping
        {
            SheetName = candidate.SheetName,
            HeaderRowNumber = candidate.HeaderRowNumber,
            Columns = columns,
            ExistingItems = existing,
            DecimalSeparator = DecimalSeparatorMode.Auto
        };
    }

    private static void CreateWorkbook(string path, string[] headers, object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Price List");
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        for (var row = 0; row < rows.Length; row++)
        for (var column = 0; column < rows[row].Length; column++)
            sheet.Cell(row + 2, column + 1).Value = XLCellValue.FromObject(rows[row][column]);
        workbook.SaveAs(path);
    }

    private sealed class ImportFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public string Folder { get; }
        public InvoiceDbContext Db { get; }
        public Supplier Supplier { get; }
        public SupplierPriceListImportService Service { get; }

        private ImportFixture(string folder, SqliteConnection connection, InvoiceDbContext db, Supplier supplier)
        {
            Folder = folder;
            this.connection = connection;
            Db = db;
            Supplier = supplier;
            Service = new SupplierPriceListImportService(db);
        }

        public static async Task<ImportFixture> CreateAsync(DbCommandInterceptor? interceptor = null)
        {
            var folder = Path.Combine(Path.GetTempPath(), $"supplier-price-list-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            var db = new InvoiceDbContext(options.Options);
            await db.Database.EnsureCreatedAsync();
            var supplier = new Supplier { CompanyName = "Test Supplier", IsActive = true };
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
            return new ImportFixture(folder, connection, db, supplier);
        }

        public string PathFor(string fileName) => Path.Combine(Folder, fileName);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
        }
    }

    private sealed class SelectCommandCounter : DbCommandInterceptor
    {
        public int SelectCount { get; private set; }

        public void Reset() => SelectCount = 0;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) SelectCount++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
