using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Services;

public sealed class PriceListService : IPriceListService
{
    private readonly InvoiceDbContext db;
    private readonly string imageFolder;

    public PriceListService(InvoiceDbContext db)
        : this(db, Path.Combine(DatabasePaths.AppDataFolder, "Assets", "PriceListImages"))
    {
    }

    public PriceListService(InvoiceDbContext db, string imageFolder)
    {
        this.db = db;
        this.imageFolder = imageFolder;
    }

    private static readonly string[] Headers =
    [
        "Image", "ReferenceNumber", "ProductName", "Description", "Category",
        "Brand", "Unit", "SellingPrice", "Currency", "TaxRate", "Supplier", "ImagePath"
    ];

    public Task<List<PriceListItem>> SearchAsync(string? text = null, CancellationToken cancellationToken = default)
    {
        var query = db.PriceListItems.AsNoTracking().Include(x => x.Supplier).Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = $"%{text.Trim()}%";
            query = query.Where(x =>
                EF.Functions.Like(x.ReferenceNumber, pattern) ||
                EF.Functions.Like(x.ProductName, pattern) ||
                EF.Functions.Like(x.Description, pattern) ||
                (x.Category != null && EF.Functions.Like(x.Category, pattern)) ||
                (x.Brand != null && EF.Functions.Like(x.Brand, pattern)));
        }

        return query.OrderBy(x => x.ReferenceNumber).ToListAsync(cancellationToken);
    }

    public async Task<PriceListItem> SaveAsync(PriceListItem item, CancellationToken cancellationToken = default)
    {
        Validate(item);
        item.ReferenceNumber = item.ReferenceNumber.Trim();
        item.ProductName = item.ProductName.Trim();
        item.Description = string.IsNullOrWhiteSpace(item.Description) ? item.ProductName : item.Description.Trim();
        item.Unit = string.IsNullOrWhiteSpace(item.Unit) ? "Piece" : item.Unit.Trim();

        var duplicate = await db.PriceListItems
            .AnyAsync(x => x.Id != item.Id && x.ReferenceNumber.ToLower() == item.ReferenceNumber.ToLower(), cancellationToken);
        if (duplicate) throw new InvalidOperationException($"Price-list reference '{item.ReferenceNumber}' already exists.");

        if (item.Id == 0) db.PriceListItems.Add(item);
        else db.PriceListItems.Update(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task DeleteAsync(int itemId, CancellationToken cancellationToken = default)
    {
        var item = await db.PriceListItems.SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        if (item is null) return;
        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountSupplierItemsAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        if (supplierId <= 0) throw new ArgumentOutOfRangeException(nameof(supplierId));
        return db.PriceListItems.CountAsync(x => x.SupplierId == supplierId, cancellationToken);
    }

    public Task<int> DeleteSupplierItemsAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        if (supplierId <= 0) throw new ArgumentOutOfRangeException(nameof(supplierId));
        var deletedAt = DateTime.UtcNow;
        return db.PriceListItems.Where(x => x.SupplierId == supplierId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.IsDeleted, true)
                .SetProperty(x => x.DeletedAt, deletedAt), cancellationToken);
    }

    public Task ExportExcelAsync(string filePath, IEnumerable<PriceListItem> items, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Price List");
        for (var column = 0; column < Headers.Length; column++)
            sheet.Cell(1, column + 1).Value = Headers[column];

        var headerRange = sheet.Range(1, 1, 1, Headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("1E83C6");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(1).Height = 24;
        sheet.SheetView.FreezeRows(1);

        var row = 2;
        foreach (var item in items.Where(x => x.IsActive).OrderBy(x => x.ReferenceNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();
            sheet.Row(row).Height = 48;
            sheet.Cell(row, 2).Value = item.ReferenceNumber;
            sheet.Cell(row, 3).Value = item.ProductName;
            sheet.Cell(row, 4).Value = item.Description;
            sheet.Cell(row, 5).Value = item.Category ?? "";
            sheet.Cell(row, 6).Value = item.Brand ?? "";
            sheet.Cell(row, 7).Value = item.Unit;
            sheet.Cell(row, 8).Value = item.SellingPrice;
            sheet.Cell(row, 9).Value = item.Currency ?? "";
            sheet.Cell(row, 10).Value = item.TaxRate;
            sheet.Cell(row, 11).Value = item.Supplier?.CompanyName ?? "";
            sheet.Cell(row, 12).Value = item.ImagePath ?? "";
            if (!string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
                sheet.AddPicture(item.ImagePath).MoveTo(sheet.Cell(row, 1)).WithSize(42, 42);
            row++;
        }

        sheet.Column(1).Width = 10;
        sheet.Column(2).Width = 18;
        sheet.Column(3).Width = 30;
        sheet.Column(4).Width = 44;
        sheet.Column(5).Width = 20;
        sheet.Column(6).Width = 20;
        sheet.Column(7).Width = 12;
        sheet.Column(8).Width = 15;
        sheet.Column(9).Width = 12;
        sheet.Column(10).Width = 12;
        sheet.Column(11).Width = 24;
        sheet.Column(12).Hide();
        sheet.Column(8).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(10).Style.NumberFormat.Format = "0.00";
        sheet.RangeUsed()?.SetAutoFilter();

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        workbook.SaveAs(filePath);
        return Task.CompletedTask;
    }

    public async Task<int> ImportExcelAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.FirstOrDefault()
                    ?? throw new InvalidOperationException("The workbook has no worksheet.");
        var header = ReadHeader(sheet);
        foreach (var required in new[] { "ReferenceNumber", "ProductName", "SellingPrice" })
            if (!header.ContainsKey(required))
                throw new InvalidOperationException("The workbook must contain ReferenceNumber, ProductName, and SellingPrice columns. Export a Price List first to get the supported template.");

        var picturesByRow = sheet.Pictures
            .Where(p => p.TopLeftCell is not null)
            .GroupBy(p => p.TopLeftCell.Address.RowNumber)
            .ToDictionary(group => group.Key, group => group.First());
        var existingItems = await db.PriceListItems.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var byReference = existingItems.ToDictionary(x => x.ReferenceNumber, StringComparer.OrdinalIgnoreCase);
        var workbookReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imported = 0;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var reference = Text(row, header, "ReferenceNumber");
                var productName = Text(row, header, "ProductName");
                var hasAnyContent = row.CellsUsed().Any(cell => !cell.IsEmpty());
                if (!hasAnyContent) continue;
                if (string.IsNullOrWhiteSpace(reference))
                    throw new InvalidOperationException($"Row {row.RowNumber()} is missing ReferenceNumber.");
                if (string.IsNullOrWhiteSpace(productName))
                    throw new InvalidOperationException($"Row {row.RowNumber()} is missing ProductName.");
                if (!workbookReferences.Add(reference))
                    throw new InvalidOperationException($"ReferenceNumber '{reference}' appears more than once in the workbook.");

                var sellingPrice = Decimal(row, header, "SellingPrice", required: true);
                var taxRate = Decimal(row, header, "TaxRate", required: false);
                if (sellingPrice < 0) throw new InvalidOperationException($"Row {row.RowNumber()} has a negative SellingPrice.");
                if (taxRate is < 0 or > 100) throw new InvalidOperationException($"Row {row.RowNumber()} has a TaxRate outside 0 to 100.");

                if (!byReference.TryGetValue(reference, out var item))
                {
                    item = new PriceListItem { ReferenceNumber = reference };
                    db.PriceListItems.Add(item);
                    byReference[reference] = item;
                }
                item.IsDeleted = false;
                item.DeletedAt = null;
                item.IsActive = true;
                item.ProductName = productName;
                item.Description = Text(row, header, "Description");
                if (string.IsNullOrWhiteSpace(item.Description)) item.Description = productName;
                item.Category = NullIfWhiteSpace(Text(row, header, "Category"));
                item.Brand = NullIfWhiteSpace(Text(row, header, "Brand"));
                item.Unit = NullIfWhiteSpace(Text(row, header, "Unit")) ?? "Piece";
                item.SellingPrice = sellingPrice;
                item.Currency = NullIfWhiteSpace(Text(row, header, "Currency"));
                item.TaxRate = taxRate;

                var imagePath = Text(row, header, "ImagePath");
                if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                    item.ImagePath = CopyImage(imagePath, reference);
                else if (picturesByRow.TryGetValue(row.RowNumber(), out var picture))
                    item.ImagePath = CopyEmbeddedImage(picture, reference);
                imported++;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return imported;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public string CopyImage(string sourcePath, string referenceNumber)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("The selected image no longer exists.", sourcePath);
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".bmp")
            throw new InvalidOperationException("Use a PNG, JPG, JPEG, or BMP image.");
        var target = NewImagePath(referenceNumber, extension);
        File.Copy(sourcePath, target, overwrite: false);
        return target;
    }

    private static Dictionary<string, int> ReadHeader(IXLWorksheet sheet)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            var name = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!result.TryAdd(name, cell.Address.ColumnNumber))
                throw new InvalidOperationException($"The workbook contains duplicate '{name}' columns.");
        }
        return result;
    }

    private static string Text(IXLRow row, IReadOnlyDictionary<string, int> header, string name)
        => header.TryGetValue(name, out var column) ? row.Cell(column).GetString().Trim() : "";

    private static decimal Decimal(IXLRow row, IReadOnlyDictionary<string, int> header, string name, bool required)
    {
        if (!header.TryGetValue(name, out var column))
        {
            if (required) throw new InvalidOperationException($"The workbook is missing the {name} column.");
            return 0;
        }
        var cell = row.Cell(column);
        if (cell.IsEmpty())
        {
            if (required) throw new InvalidOperationException($"Row {row.RowNumber()} is missing {name}.");
            return 0;
        }
        if (cell.TryGetValue<decimal>(out var value)) return value;
        throw new InvalidOperationException($"Row {row.RowNumber()} has an invalid {name}.");
    }

    private string CopyEmbeddedImage(IXLPicture picture, string referenceNumber)
    {
        var extension = picture.Format switch
        {
            XLPictureFormat.Bmp => ".bmp",
            XLPictureFormat.Jpeg => ".jpg",
            XLPictureFormat.Gif => ".gif",
            XLPictureFormat.Tiff => ".tiff",
            _ => ".png"
        };
        var target = NewImagePath(referenceNumber, extension);
        using var source = picture.ImageStream;
        if (source.CanSeek) source.Position = 0;
        using var destination = File.Create(target);
        source.CopyTo(destination);
        return target;
    }

    private string NewImagePath(string referenceNumber, string extension)
    {
        Directory.CreateDirectory(imageFolder);
        var safeReference = string.Concat(referenceNumber.Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(safeReference)) safeReference = "item";
        return Path.Combine(imageFolder, $"{safeReference}-{Guid.NewGuid():N}{extension}");
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static void Validate(PriceListItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ReferenceNumber)) throw new InvalidOperationException("Reference number is required.");
        if (string.IsNullOrWhiteSpace(item.ProductName)) throw new InvalidOperationException("Product name is required.");
        if (item.SellingPrice < 0) throw new InvalidOperationException("Selling price cannot be negative.");
        if (item.TaxRate is < 0 or > 100) throw new InvalidOperationException("Tax rate must be between 0 and 100.");
    }
}
