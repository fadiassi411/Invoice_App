using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ExcelDataReader;
using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Services;

public sealed class SupplierPriceListImportService : ISupplierPriceListImportService
{
    private const int MaximumHeaderScanRows = 500;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<PriceListImportField, string[]> Aliases =
        new Dictionary<PriceListImportField, string[]>
        {
            [PriceListImportField.ItemNumber] = ["reference", "reference number", "item number", "item no", "item code", "sku", "product code", "part number", "part no", "catalog number", "catalogue number", "code"],
            [PriceListImportField.ProductName] = ["product name", "item name", "product", "name"],
            [PriceListImportField.Description] = ["description", "item description", "product description", "details"],
            [PriceListImportField.Price] = ["price", "unit price", "unit cost", "cost", "selling price", "retail price", "wholesale price", "discounted price", "net price", "list price"],
            [PriceListImportField.Currency] = ["currency", "currency code", "curr"],
            [PriceListImportField.Category] = ["category", "product category", "group"],
            [PriceListImportField.Brand] = ["brand", "manufacturer", "make"],
            [PriceListImportField.Unit] = ["unit", "uom", "unit of measurement", "unit measure"],
            [PriceListImportField.TaxRate] = ["tax", "tax rate", "tax percent", "tax percentage", "vat", "vat rate"]
        };

    private readonly InvoiceDbContext db;

    public SupplierPriceListImportService(InvoiceDbContext db) => this.db = db;

    public async Task<PriceListWorkbookAnalysis> AnalyzeAsync(string filePath, int supplierId, CancellationToken cancellationToken = default)
    {
        ValidateFile(filePath);
        _ = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken)
            ?? throw new InvalidOperationException("Select a valid supplier before detecting the workbook.");

        var workbook = ReadWorkbook(filePath, cancellationToken);
        var candidates = DetectCandidates(workbook);
        if (candidates.Count == 0)
            throw new InvalidOperationException("No possible table header was found. Select a sheet and header row manually after checking that the file contains item, description, and price columns.");

        var saved = await db.SupplierPriceListMappings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SupplierId == supplierId, cancellationToken);
        if (saved is null)
            return new PriceListWorkbookAnalysis { FilePath = filePath, Candidates = candidates };

        var savedHeaders = Deserialize<Dictionary<PriceListImportField, string>>(saved.ColumnMappingsJson) ?? [];
        var savedSettings = Deserialize<SavedParsingSettings>(saved.ParsingSettingsJson) ?? new();
        var matchingCandidate = candidates
            .Where(x => string.Equals(x.SheetName, saved.SheetName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => savedHeaders.Values.Count(savedHeader => x.Columns.Count(c => Normalize(c.Header) == Normalize(savedHeader)) == 1))
            .ThenBy(x => Math.Abs(x.HeaderRowNumber - saved.HeaderRowNumber))
            .FirstOrDefault();
        if (matchingCandidate is null)
        {
            return new PriceListWorkbookAnalysis
            {
                FilePath = filePath,
                Candidates = candidates,
                SavedMappingMessage = $"The saved sheet '{saved.SheetName}' is not present. Confirm a new mapping."
            };
        }

        var remapped = new Dictionary<PriceListImportField, int>();
        var missing = new List<string>();
        var ambiguous = new List<string>();
        foreach (var entry in savedHeaders)
        {
            var matches = matchingCandidate.Columns.Where(x => Normalize(x.Header) == Normalize(entry.Value)).ToList();
            if (matches.Count == 0) missing.Add(entry.Value);
            else if (matches.Count > 1) ambiguous.Add(entry.Value);
            else remapped[entry.Key] = matches[0].ColumnIndex;
        }

        if (missing.Count > 0 || ambiguous.Count > 0 || !HasRequiredMappings(remapped))
        {
            var details = new List<string>();
            if (missing.Count > 0) details.Add($"missing: {string.Join(", ", missing)}");
            if (ambiguous.Count > 0) details.Add($"repeated: {string.Join(", ", ambiguous)}");
            return new PriceListWorkbookAnalysis
            {
                FilePath = filePath,
                Candidates = candidates,
                SavedMappingMessage = $"The supplier layout changed ({string.Join("; ", details)}). Confirm the new mapping."
            };
        }

        var reusable = new PriceListImportMapping
        {
            SheetName = matchingCandidate.SheetName,
            HeaderRowNumber = matchingCandidate.HeaderRowNumber,
            Columns = remapped,
            DecimalSeparator = savedSettings.DecimalSeparator,
            ExistingItems = savedSettings.ExistingItems
        };
        return new PriceListWorkbookAnalysis
        {
            FilePath = filePath,
            Candidates = candidates,
            SavedMappingApplied = true,
            SavedMappingMessage = "The saved supplier mapping matched this workbook and was reapplied by header name.",
            SavedMapping = reusable
        };
    }

    public async Task<PriceListImportPreview> BuildPreviewAsync(
        string filePath,
        int supplierId,
        PriceListImportMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ValidateFile(filePath);
        ValidateMapping(mapping);
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken)
            ?? throw new InvalidOperationException("The selected supplier no longer exists.");
        var workbook = ReadWorkbook(filePath, cancellationToken);
        var sheet = workbook.Sheets.FirstOrDefault(x => string.Equals(x.Name, mapping.SheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Sheet '{mapping.SheetName}' was not found.");
        if (mapping.HeaderRowNumber < 1 || mapping.HeaderRowNumber > sheet.Rows.Count)
            throw new InvalidOperationException("The selected header row is outside the worksheet.");

        var header = sheet.Rows[mapping.HeaderRowNumber - 1];
        var preview = new PriceListImportPreview
        {
            SupplierId = supplierId,
            SupplierName = supplier.CompanyName,
            FilePath = filePath,
            Mapping = mapping,
            HeaderNames = header.Cells.Select(x => x.DisplayText).ToList()
        };

        var existing = await db.PriceListItems.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(x => x.ReferenceNumber, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var recordsByReference = new Dictionary<string, List<PriceListImportRecord>>(StringComparer.OrdinalIgnoreCase);

        for (var rowIndex = mapping.HeaderRowNumber; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceRow = sheet.Rows[rowIndex];
            if (sourceRow.Cells.All(IsClearlyEmpty)) continue;
            if (IsRepeatedHeader(sourceRow, header, mapping.Columns)) continue;

            var record = new PriceListImportRecord { SheetName = sheet.Name, ExcelRowNumber = sourceRow.ExcelRowNumber };
            var itemCell = Cell(sourceRow, mapping, PriceListImportField.ItemNumber);
            record.ReferenceNumber = ParseIdentifier(itemCell, record, preview);
            var descriptionCell = Cell(sourceRow, mapping, PriceListImportField.Description);
            var nameCell = Cell(sourceRow, mapping, PriceListImportField.ProductName);
            record.Description = CleanText(descriptionCell?.DisplayText);
            record.ProductName = CleanText(nameCell?.DisplayText);
            if (string.IsNullOrWhiteSpace(record.ProductName) && !string.IsNullOrWhiteSpace(record.Description))
            {
                record.ProductName = record.Description;
                AddIssue(record, preview, ImportIssueSeverity.Warning, "Product name", descriptionCell, "No separate product-name column was mapped; the description will be used as the item name.");
            }
            if (string.IsNullOrWhiteSpace(record.Description) && !string.IsNullOrWhiteSpace(record.ProductName))
                record.Description = record.ProductName;
            if (string.IsNullOrWhiteSpace(record.ReferenceNumber))
                AddIssue(record, preview, ImportIssueSeverity.Error, "Item number", itemCell, "Item number is required.");
            if (string.IsNullOrWhiteSpace(record.ProductName))
                AddIssue(record, preview, ImportIssueSeverity.Error, "Description", descriptionCell ?? nameCell, "Description or product name is required.");

            var priceCell = Cell(sourceRow, mapping, PriceListImportField.Price);
            record.Price = ParseDecimal(priceCell, mapping.DecimalSeparator, required: true, "Price", record, preview);
            if (record.Price is < 0)
                AddIssue(record, preview, ImportIssueSeverity.Error, "Price", priceCell, "Price cannot be negative.");

            record.Currency = OptionalText(sourceRow, mapping, PriceListImportField.Currency);
            record.Category = OptionalText(sourceRow, mapping, PriceListImportField.Category);
            record.Brand = OptionalText(sourceRow, mapping, PriceListImportField.Brand);
            record.Unit = OptionalText(sourceRow, mapping, PriceListImportField.Unit);
            var taxCell = Cell(sourceRow, mapping, PriceListImportField.TaxRate);
            if (taxCell is not null && !IsClearlyEmpty(taxCell))
            {
                record.TaxRate = ParseDecimal(taxCell, mapping.DecimalSeparator, required: false, "Tax rate", record, preview);
                if (record.TaxRate is < 0 or > 100)
                    AddIssue(record, preview, ImportIssueSeverity.Error, "Tax rate", taxCell, "Tax rate must be between 0 and 100.");
            }

            foreach (var mappedCell in mapping.Columns.Values.Distinct().Select(index => Cell(sourceRow, index)).Where(x => x is not null))
            {
                if (!string.IsNullOrWhiteSpace(mappedCell!.Error))
                    AddIssue(record, preview, ImportIssueSeverity.Error, "Excel cell", mappedCell, $"Excel error cell: {mappedCell.Error}.");
                if (mappedCell.HasFormula && !mappedCell.HasReadableValue)
                    AddIssue(record, preview, ImportIssueSeverity.Error, "Formula", mappedCell, "Formula has no saved calculated value. Recalculate and save the workbook in Excel, then try again.");
            }

            if (!string.IsNullOrWhiteSpace(record.ReferenceNumber) && existing.TryGetValue(record.ReferenceNumber, out var existingItem))
            {
                record.ExistingItemId = existingItem.Id;
                switch (mapping.ExistingItems)
                {
                    case ExistingItemMode.UpdateMatching:
                        record.Action = ImportRecordAction.Update;
                        break;
                    case ExistingItemMode.SkipMatching:
                        record.Action = ImportRecordAction.Skip;
                        AddIssue(record, preview, ImportIssueSeverity.Warning, "Existing item", itemCell, "The reference already exists and will be skipped by the selected rule.");
                        break;
                    default:
                        record.Action = ImportRecordAction.Skip;
                        AddIssue(record, preview, ImportIssueSeverity.Error, "Existing item", itemCell, "The reference already exists. Choose Update matching items or Skip matching items explicitly.");
                        break;
                }
            }
            else record.Action = ImportRecordAction.Create;

            preview.Records.Add(record);
            if (!string.IsNullOrWhiteSpace(record.ReferenceNumber))
            {
                if (!recordsByReference.TryGetValue(record.ReferenceNumber, out var group))
                    recordsByReference[record.ReferenceNumber] = group = [];
                group.Add(record);
            }
        }

        foreach (var duplicate in recordsByReference.Where(x => x.Value.Count > 1))
        {
            foreach (var record in duplicate.Value)
                AddIssue(record, preview, ImportIssueSeverity.Error, "Item number", null, $"Item number '{duplicate.Key}' appears more than once in this file.", duplicate.Key);
        }

        if (!mapping.Columns.ContainsKey(PriceListImportField.Currency))
        {
            preview.Issues.Add(new PriceListImportIssue
            {
                Severity = ImportIssueSeverity.Warning,
                SheetName = sheet.Name,
                ExcelRowNumber = mapping.HeaderRowNumber,
                Field = "Currency",
                Message = "No currency column is mapped. Existing currency values will remain unchanged; new items will have no currency."
            });
        }
        if (!mapping.Columns.ContainsKey(PriceListImportField.TaxRate))
        {
            preview.Issues.Add(new PriceListImportIssue
            {
                Severity = ImportIssueSeverity.Warning,
                SheetName = sheet.Name,
                ExcelRowNumber = mapping.HeaderRowNumber,
                Field = "Tax rate",
                Message = "No tax column is mapped. Existing tax values will remain unchanged; new items will use the current price-list default of 0%."
            });
        }

        return preview;
    }

    public async Task<PriceListImportResult> ImportAsync(PriceListImportPreview preview, bool importValidRecordsOnly, CancellationToken cancellationToken = default)
    {
        if (preview.Records.Count == 0) throw new InvalidOperationException("There are no data records to import.");
        if (!importValidRecordsOnly && preview.Records.Any(x => x.HasErrors || x.Action == ImportRecordAction.Skip))
            throw new InvalidOperationException("The preview contains errors or skipped records. Correct them, or explicitly select Import valid records only.");

        var importable = preview.Records.Where(x => x.CanImport).ToList();
        if (importable.Count == 0) throw new InvalidOperationException("There are no valid records to import.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == preview.SupplierId, cancellationToken)
                ?? throw new InvalidOperationException("The selected supplier no longer exists.");
            // Revalidate the preview with one database read. Querying once per row makes
            // large supplier lists progressively slower and holds the UI open much longer.
            var currentItems = await db.PriceListItems.IgnoreQueryFilters().ToListAsync(cancellationToken);
            var currentByReference = currentItems.ToDictionary(x => x.ReferenceNumber, StringComparer.OrdinalIgnoreCase);
            var newItems = new List<PriceListItem>();
            var created = 0;
            var updated = 0;
            foreach (var record in importable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PriceListItem item;
                if (record.Action == ImportRecordAction.Update)
                {
                    if (!currentByReference.TryGetValue(record.ReferenceNumber, out item!) || item.Id != record.ExistingItemId)
                        throw new DbUpdateConcurrencyException($"Price-list item '{record.ReferenceNumber}' was changed or removed after preview.");
                    updated++;
                }
                else
                {
                    if (currentByReference.ContainsKey(record.ReferenceNumber))
                        throw new DbUpdateConcurrencyException($"Price-list item '{record.ReferenceNumber}' was added after preview. Regenerate the preview.");
                    item = new PriceListItem { ReferenceNumber = record.ReferenceNumber };
                    currentByReference.Add(record.ReferenceNumber, item);
                    newItems.Add(item);
                    created++;
                }

                item.IsDeleted = false;
                item.DeletedAt = null;
                item.IsActive = true;
                item.SupplierId = supplier.Id;
                item.ProductName = record.ProductName;
                item.Description = record.Description;
                item.SellingPrice = record.Price!.Value;
                if (preview.Mapping.Columns.ContainsKey(PriceListImportField.Currency)) item.Currency = NullIfWhiteSpace(record.Currency);
                if (preview.Mapping.Columns.ContainsKey(PriceListImportField.Category)) item.Category = NullIfWhiteSpace(record.Category);
                if (preview.Mapping.Columns.ContainsKey(PriceListImportField.Brand)) item.Brand = NullIfWhiteSpace(record.Brand);
                if (preview.Mapping.Columns.ContainsKey(PriceListImportField.Unit)) item.Unit = NullIfWhiteSpace(record.Unit) ?? "Piece";
                else if (record.Action == ImportRecordAction.Create) item.Unit = "Piece";
                if (preview.Mapping.Columns.ContainsKey(PriceListImportField.TaxRate) && record.TaxRate.HasValue) item.TaxRate = record.TaxRate.Value;
            }

            if (newItems.Count > 0) db.PriceListItems.AddRange(newItems);
            await SaveMappingAsync(preview, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PriceListImportResult
            {
                Created = created,
                Updated = updated,
                Skipped = preview.Records.Count - importable.Count
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task ResetSavedMappingAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        var mapping = await db.SupplierPriceListMappings.SingleOrDefaultAsync(x => x.SupplierId == supplierId, cancellationToken);
        if (mapping is null) return;
        db.SupplierPriceListMappings.Remove(mapping);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveMappingAsync(PriceListImportPreview preview, CancellationToken cancellationToken)
    {
        var entity = await db.SupplierPriceListMappings.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.SupplierId == preview.SupplierId, cancellationToken);
        if (entity is null)
        {
            entity = new SupplierPriceListMapping { SupplierId = preview.SupplierId };
            db.SupplierPriceListMappings.Add(entity);
        }
        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.SheetName = preview.Mapping.SheetName;
        entity.HeaderRowNumber = preview.Mapping.HeaderRowNumber;
        entity.HeaderNamesJson = JsonSerializer.Serialize(preview.HeaderNames, JsonOptions);
        var columnHeaders = preview.Mapping.Columns.ToDictionary(
            x => x.Key,
            x => x.Value >= 0 && x.Value < preview.HeaderNames.Count ? preview.HeaderNames[x.Value] : "");
        entity.ColumnMappingsJson = JsonSerializer.Serialize(columnHeaders, JsonOptions);
        entity.ParsingSettingsJson = JsonSerializer.Serialize(new SavedParsingSettings
        {
            DecimalSeparator = preview.Mapping.DecimalSeparator,
            ExistingItems = preview.Mapping.ExistingItems
        }, JsonOptions);
    }

    private static List<PriceListTableCandidate> DetectCandidates(WorkbookSnapshot workbook)
    {
        var result = new List<PriceListTableCandidate>();
        foreach (var sheet in workbook.Sheets)
        {
            var sheetCandidates = new List<PriceListTableCandidate>();
            foreach (var row in sheet.Rows.Take(MaximumHeaderScanRows))
            {
                var columns = row.Cells
                    .Where(x => !string.IsNullOrWhiteSpace(x.DisplayText))
                    .Select(x => new PriceListColumnOption
                    {
                        ColumnIndex = x.ColumnIndex,
                        Header = x.DisplayText.Trim(),
                        Samples = BuildSamples(sheet, row.ExcelRowNumber, x.ColumnIndex)
                    }).ToList();
                if (columns.Count < 2) continue;
                var suggestions = SuggestMappings(columns);
                var score = Score(suggestions, columns);
                if (score < 4) continue;
                var priceMatches = columns.Count(x => MatchAlias(PriceListImportField.Price, x.Header));
                sheetCandidates.Add(new PriceListTableCandidate
                {
                    SheetName = sheet.Name,
                    HeaderRowNumber = row.ExcelRowNumber,
                    Score = score,
                    Columns = columns,
                    SuggestedMappings = suggestions,
                    RequiresConfirmation = priceMatches > 1 || !HasRequiredMappings(suggestions)
                });
            }

            result.AddRange(sheetCandidates.OrderByDescending(x => x.Score).ThenBy(x => x.HeaderRowNumber).Take(3));
            if (sheetCandidates.Count == 0)
            {
                var first = sheet.Rows.Take(MaximumHeaderScanRows).FirstOrDefault(x => x.Cells.Count(c => !IsClearlyEmpty(c)) >= 2);
                if (first is not null)
                {
                    var columns = first.Cells.Where(x => !IsClearlyEmpty(x)).Select(x => new PriceListColumnOption
                    {
                        ColumnIndex = x.ColumnIndex,
                        Header = x.DisplayText.Trim(),
                        Samples = BuildSamples(sheet, first.ExcelRowNumber, x.ColumnIndex)
                    }).ToList();
                    result.Add(new PriceListTableCandidate
                    {
                        SheetName = sheet.Name,
                        HeaderRowNumber = first.ExcelRowNumber,
                        Score = 0,
                        Columns = columns,
                        SuggestedMappings = SuggestMappings(columns),
                        RequiresConfirmation = true
                    });
                }
            }
        }
        return result.OrderByDescending(x => x.Score).ThenBy(x => x.SheetName).ThenBy(x => x.HeaderRowNumber).ToList();
    }

    private static Dictionary<PriceListImportField, int> SuggestMappings(IReadOnlyList<PriceListColumnOption> columns)
    {
        var result = new Dictionary<PriceListImportField, int>();
        foreach (var field in Aliases.Keys)
        {
            var matches = columns.Where(x => MatchAlias(field, x.Header)).ToList();
            if (matches.Count == 1) result[field] = matches[0].ColumnIndex;
            else if (matches.Count > 1 && field != PriceListImportField.Price) result[field] = matches[0].ColumnIndex;
        }
        return result;
    }

    private static int Score(IReadOnlyDictionary<PriceListImportField, int> suggestions, IReadOnlyList<PriceListColumnOption> columns)
    {
        var score = 0;
        if (suggestions.ContainsKey(PriceListImportField.ItemNumber)) score += 5;
        if (columns.Any(x => MatchAlias(PriceListImportField.Price, x.Header))) score += 5;
        if (suggestions.ContainsKey(PriceListImportField.Description)) score += 3;
        if (suggestions.ContainsKey(PriceListImportField.ProductName)) score += 2;
        score += suggestions.Keys.Count(x => x is PriceListImportField.Currency or PriceListImportField.Category or PriceListImportField.Brand or PriceListImportField.Unit or PriceListImportField.TaxRate);
        return score;
    }

    private static bool HasRequiredMappings(IReadOnlyDictionary<PriceListImportField, int> mappings)
        => mappings.ContainsKey(PriceListImportField.ItemNumber)
           && mappings.ContainsKey(PriceListImportField.Price)
           && (mappings.ContainsKey(PriceListImportField.Description) || mappings.ContainsKey(PriceListImportField.ProductName));

    private static void ValidateMapping(PriceListImportMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.SheetName)) throw new InvalidOperationException("Select a worksheet/table.");
        if (!HasRequiredMappings(mapping.Columns))
            throw new InvalidOperationException("Confirm Item number, Price, and at least one Description or Product name mapping before previewing.");
        if (mapping.Columns.GroupBy(x => x.Value).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Each Excel column can map to only one app field.");
    }

    private static WorkbookSnapshot ReadWorkbook(string filePath, CancellationToken cancellationToken)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration
        {
            FallbackEncoding = Encoding.GetEncoding(1252),
            LeaveOpen = false
        });
        var sheets = new List<SheetSnapshot>();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = new List<RowSnapshot>();
            var rowNumber = 0;
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;
                var cells = new List<CellSnapshot>();
                var fieldCount = reader.FieldCount;
                for (var column = 0; column < fieldCount; column++)
                {
                    var raw = reader.GetValue(column);
                    var format = reader.GetNumberFormatString(column);
                    var error = reader.GetCellError(column)?.ToString();
                    var display = Display(raw, format);
                    cells.Add(new CellSnapshot
                    {
                        ColumnIndex = column,
                        ExcelRowNumber = rowNumber,
                        RawValue = raw,
                        DisplayText = display,
                        NumberFormat = format,
                        Error = error,
                        HasReadableValue = raw is not null && string.IsNullOrWhiteSpace(error),
                        // ExcelDataReader returns the saved formula result. A null result is
                        // treated as unreadable even when the legacy format cannot expose the formula text.
                        HasFormula = false
                    });
                }
                rows.Add(new RowSnapshot { ExcelRowNumber = rowNumber, Cells = cells });
            }
            sheets.Add(new SheetSnapshot { Name = reader.Name, Rows = rows });
        } while (reader.NextResult());
        if (Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            EnrichFormulaMetadata(filePath, sheets);
        return new WorkbookSnapshot { Sheets = sheets };
    }

    private static void EnrichFormulaMetadata(string filePath, IReadOnlyList<SheetSnapshot> sheets)
    {
        using var workbook = new XLWorkbook(filePath, new LoadOptions { RecalculateAllFormulas = false });
        foreach (var worksheet in workbook.Worksheets)
        {
            var sheet = sheets.FirstOrDefault(x => string.Equals(x.Name, worksheet.Name, StringComparison.OrdinalIgnoreCase));
            if (sheet is null) continue;
            foreach (var formulaCell in worksheet.CellsUsed().Where(x => x.HasFormula))
            {
                var rowIndex = formulaCell.Address.RowNumber - 1;
                var columnIndex = formulaCell.Address.ColumnNumber - 1;
                if (rowIndex < 0 || rowIndex >= sheet.Rows.Count) continue;
                var cell = Cell(sheet.Rows[rowIndex], columnIndex);
                if (cell is null) continue;
                cell.HasFormula = true;
                var cached = formulaCell.CachedValue;
                cell.HasReadableValue = !cached.IsBlank && !cached.IsError;
                if (cached.IsError) cell.Error ??= cached.GetError().ToString();
            }
        }
    }

    private static string Display(object? raw, string? numberFormat)
    {
        if (raw is null) return "";
        if (raw is string text) return text;
        if (raw is DateTime date) return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (raw is TimeSpan time) return time.ToString();
        if (raw is double number)
        {
            var zeroMask = ZeroMaskLength(numberFormat);
            if (zeroMask > 0 && number == Math.Truncate(number) && number >= 0)
                return number.ToString(new string('0', zeroMask), CultureInfo.InvariantCulture);
            return number.ToString("G17", CultureInfo.InvariantCulture);
        }
        if (raw is float single) return single.ToString("G9", CultureInfo.InvariantCulture);
        if (raw is decimal amount) return amount.ToString(CultureInfo.InvariantCulture);
        return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
    }

    private static int ZeroMaskLength(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return 0;
        var cleaned = Regex.Replace(format.Split(';')[0], "\"[^\"]*\"|\\\\.", "");
        return Regex.IsMatch(cleaned, "^0+$") ? cleaned.Length : 0;
    }

    private static string ParseIdentifier(CellSnapshot? cell, PriceListImportRecord record, PriceListImportPreview preview)
    {
        if (cell is null) return "";
        if (!string.IsNullOrWhiteSpace(cell.Error)) return "";
        if (cell.RawValue is double number)
        {
            if (Math.Abs(number) >= 1_000_000_000_000_000d)
                AddIssue(record, preview, ImportIssueSeverity.Warning, "Item number", cell, "This item number is stored as a large Excel number and may already have lost digits. Verify it against the supplier source.");
            var zeroMask = ZeroMaskLength(cell.NumberFormat);
            if (zeroMask > 0 && number == Math.Truncate(number) && number >= 0)
                return number.ToString(new string('0', zeroMask), CultureInfo.InvariantCulture);
            if (number == Math.Truncate(number)) return number.ToString("0", CultureInfo.InvariantCulture);
        }
        return CleanText(cell.DisplayText);
    }

    private static decimal? ParseDecimal(
        CellSnapshot? cell,
        DecimalSeparatorMode mode,
        bool required,
        string field,
        PriceListImportRecord record,
        PriceListImportPreview preview)
    {
        if (cell is null || IsClearlyEmpty(cell))
        {
            if (required) AddIssue(record, preview, ImportIssueSeverity.Error, field, cell, $"{field} is required.");
            return null;
        }
        if (!string.IsNullOrWhiteSpace(cell.Error))
        {
            AddIssue(record, preview, ImportIssueSeverity.Error, field, cell, $"Excel error cell: {cell.Error}.");
            return null;
        }
        try
        {
            if (cell.RawValue is double number) return checked((decimal)number);
            if (cell.RawValue is decimal amount) return amount;
            if (cell.RawValue is int integer) return integer;
            if (cell.RawValue is long longInteger) return longInteger;
        }
        catch (OverflowException)
        {
            AddIssue(record, preview, ImportIssueSeverity.Error, field, cell, $"{field} is outside the supported numeric range.");
            return null;
        }

        var text = CleanText(cell.DisplayText).Replace(" ", "").Replace("\u00A0", "");
        if (string.IsNullOrWhiteSpace(text))
        {
            if (required) AddIssue(record, preview, ImportIssueSeverity.Error, field, cell, $"{field} is required.");
            return null;
        }
        text = Regex.Replace(text, "[^0-9,.*+\\-]", "");
        var commaCount = text.Count(x => x == ',');
        var dotCount = text.Count(x => x == '.');
        string normalized;
        if (mode == DecimalSeparatorMode.Dot)
            normalized = text.Replace(",", "");
        else if (mode == DecimalSeparatorMode.Comma)
            normalized = text.Replace(".", "").Replace(',', '.');
        else if (commaCount > 0 && dotCount > 0)
        {
            var decimalSeparator = text.LastIndexOf(',') > text.LastIndexOf('.') ? ',' : '.';
            normalized = decimalSeparator == ',' ? text.Replace(".", "").Replace(',', '.') : text.Replace(",", "");
        }
        else if (commaCount + dotCount == 1)
        {
            var separator = commaCount == 1 ? ',' : '.';
            var digitsAfter = text.Length - text.LastIndexOf(separator) - 1;
            if (digitsAfter == 3)
            {
                AddIssue(record, preview, ImportIssueSeverity.Error, field, cell, $"'{cell.DisplayText}' is ambiguous: the separator could mean thousands or decimals. Select Dot or Comma parsing explicitly.");
                return null;
            }
            normalized = separator == ',' ? text.Replace(',', '.') : text;
        }
        else if (commaCount > 1 && dotCount == 0)
            normalized = text.Replace(",", "");
        else if (dotCount > 1 && commaCount == 0)
            normalized = text.Replace(".", "");
        else normalized = text;

        if (decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        AddIssue(record, preview, ImportIssueSeverity.Error, field, cell, $"'{cell.DisplayText}' is not a valid {field.ToLowerInvariant()}.");
        return null;
    }

    private static bool IsRepeatedHeader(RowSnapshot row, RowSnapshot header, IReadOnlyDictionary<PriceListImportField, int> mappings)
    {
        var compared = 0;
        foreach (var column in mappings.Values.Distinct())
        {
            var current = Cell(row, column)?.DisplayText;
            var expected = Cell(header, column)?.DisplayText;
            if (string.IsNullOrWhiteSpace(expected)) continue;
            compared++;
            if (Normalize(current) != Normalize(expected)) return false;
        }
        return compared >= 2;
    }

    private static string? OptionalText(RowSnapshot row, PriceListImportMapping mapping, PriceListImportField field)
        => mapping.Columns.TryGetValue(field, out var column) ? NullIfWhiteSpace(CleanText(Cell(row, column)?.DisplayText)) : null;

    private static CellSnapshot? Cell(RowSnapshot row, PriceListImportMapping mapping, PriceListImportField field)
        => mapping.Columns.TryGetValue(field, out var column) ? Cell(row, column) : null;

    private static CellSnapshot? Cell(RowSnapshot row, int column)
        => column >= 0 && column < row.Cells.Count ? row.Cells[column] : null;

    private static bool MatchAlias(PriceListImportField field, string header)
    {
        var normalized = Normalize(header);
        return Aliases[field].Any(alias => Normalize(alias) == normalized);
    }

    private static string Normalize(string? value)
        => Regex.Replace(value ?? "", "[^a-z0-9]", "", RegexOptions.IgnoreCase).ToLowerInvariant();

    private static string BuildSamples(SheetSnapshot sheet, int headerRow, int column)
        => string.Join("; ", sheet.Rows.Where(x => x.ExcelRowNumber > headerRow)
            .Select(x => Cell(x, column)?.DisplayText?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(3));

    private static void AddIssue(
        PriceListImportRecord record,
        PriceListImportPreview preview,
        ImportIssueSeverity severity,
        string field,
        CellSnapshot? cell,
        string message,
        string? originalValue = null)
    {
        var issue = new PriceListImportIssue
        {
            Severity = severity,
            SheetName = record.SheetName,
            ExcelRowNumber = record.ExcelRowNumber,
            Field = field,
            OriginalValue = originalValue ?? cell?.DisplayText ?? "",
            Message = message
        };
        record.Issues.Add(issue);
        preview.Issues.Add(issue);
    }

    private static bool IsClearlyEmpty(CellSnapshot cell)
        => cell.RawValue is null && string.IsNullOrWhiteSpace(cell.DisplayText) && string.IsNullOrWhiteSpace(cell.Error);

    private static string CleanText(string? value) => (value ?? "").Trim();
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateFile(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("The selected Excel file no longer exists.", filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension is not ".xlsx" and not ".xls")
            throw new InvalidOperationException("Select an Excel .xlsx or .xls file.");
    }

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return default; }
    }

    private sealed class SavedParsingSettings
    {
        public DecimalSeparatorMode DecimalSeparator { get; set; } = DecimalSeparatorMode.Auto;
        public ExistingItemMode ExistingItems { get; set; } = ExistingItemMode.CreateOnly;
    }

    private sealed class WorkbookSnapshot
    {
        public List<SheetSnapshot> Sheets { get; init; } = [];
    }

    private sealed class SheetSnapshot
    {
        public string Name { get; init; } = "";
        public List<RowSnapshot> Rows { get; init; } = [];
    }

    private sealed class RowSnapshot
    {
        public int ExcelRowNumber { get; init; }
        public List<CellSnapshot> Cells { get; init; } = [];
    }

    private sealed class CellSnapshot
    {
        public int ColumnIndex { get; init; }
        public int ExcelRowNumber { get; init; }
        public object? RawValue { get; init; }
        public string DisplayText { get; init; } = "";
        public string? NumberFormat { get; init; }
        public string? Error { get; set; }
        public bool HasFormula { get; set; }
        public bool HasReadableValue { get; set; }
    }
}
