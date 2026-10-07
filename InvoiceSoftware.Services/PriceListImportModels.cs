using System.Collections.ObjectModel;

namespace InvoiceSoftware.Services;

public enum PriceListImportField
{
    ItemNumber,
    ProductName,
    Description,
    Price,
    Currency,
    Category,
    Brand,
    Unit,
    TaxRate
}

public enum DecimalSeparatorMode
{
    Auto,
    Dot,
    Comma
}

public enum ExistingItemMode
{
    CreateOnly,
    UpdateMatching,
    SkipMatching
}

public enum ImportIssueSeverity
{
    Warning,
    Error
}

public enum ImportRecordAction
{
    Create,
    Update,
    Skip
}

public sealed class PriceListColumnOption
{
    public int ColumnIndex { get; init; }
    public string Header { get; init; } = "";
    public string Samples { get; init; } = "";
    public override string ToString() => string.IsNullOrWhiteSpace(Samples) ? Header : $"{Header}  |  {Samples}";
}

public sealed class PriceListTableCandidate
{
    public string SheetName { get; init; } = "";
    public int HeaderRowNumber { get; init; }
    public int Score { get; init; }
    public bool RequiresConfirmation { get; init; }
    public IReadOnlyList<PriceListColumnOption> Columns { get; init; } = [];
    public IReadOnlyDictionary<PriceListImportField, int> SuggestedMappings { get; init; }
        = new Dictionary<PriceListImportField, int>();
    public override string ToString() => $"{SheetName} - header row {HeaderRowNumber}";
}

public sealed class PriceListWorkbookAnalysis
{
    public string FilePath { get; init; } = "";
    public IReadOnlyList<PriceListTableCandidate> Candidates { get; init; } = [];
    public bool SavedMappingApplied { get; init; }
    public string? SavedMappingMessage { get; init; }
    public PriceListImportMapping? SavedMapping { get; init; }
}

public sealed class PriceListImportMapping
{
    public string SheetName { get; init; } = "";
    public int HeaderRowNumber { get; init; }
    public Dictionary<PriceListImportField, int> Columns { get; init; } = [];
    public DecimalSeparatorMode DecimalSeparator { get; init; } = DecimalSeparatorMode.Auto;
    public ExistingItemMode ExistingItems { get; init; } = ExistingItemMode.CreateOnly;
}

public sealed class PriceListImportIssue
{
    public ImportIssueSeverity Severity { get; init; }
    public string SheetName { get; init; } = "";
    public int ExcelRowNumber { get; init; }
    public string Field { get; init; } = "";
    public string OriginalValue { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed class PriceListImportRecord
{
    public string SheetName { get; init; } = "";
    public int ExcelRowNumber { get; init; }
    public string ReferenceNumber { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string? Unit { get; set; }
    public decimal? TaxRate { get; set; }
    public ImportRecordAction Action { get; set; }
    public int? ExistingItemId { get; set; }
    public ObservableCollection<PriceListImportIssue> Issues { get; } = [];
    public bool HasErrors => Issues.Any(x => x.Severity == ImportIssueSeverity.Error);
    public bool CanImport => !HasErrors && Action != ImportRecordAction.Skip;
    public string IssueSummary => string.Join(" | ", Issues.Select(x => x.Message));
}

public sealed class PriceListImportPreview
{
    public int SupplierId { get; init; }
    public string SupplierName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public PriceListImportMapping Mapping { get; init; } = new();
    public IReadOnlyList<string> HeaderNames { get; init; } = [];
    public ObservableCollection<PriceListImportRecord> Records { get; init; } = [];
    public ObservableCollection<PriceListImportIssue> Issues { get; init; } = [];
    public int TotalRecords => Records.Count;
    public int ValidRecords => Records.Count(x => x.CanImport);
    public int WarningCount => Issues.Count(x => x.Severity == ImportIssueSeverity.Warning);
    public int ErrorCount => Issues.Count(x => x.Severity == ImportIssueSeverity.Error);
    public int CreateCount => Records.Count(x => x.CanImport && x.Action == ImportRecordAction.Create);
    public int UpdateCount => Records.Count(x => x.CanImport && x.Action == ImportRecordAction.Update);
}

public sealed class PriceListImportResult
{
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public int Imported => Created + Updated;
}
