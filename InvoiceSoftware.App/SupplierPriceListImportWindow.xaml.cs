using System.Windows;
using System.Windows.Controls;
using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Services;
using Microsoft.Win32;

namespace InvoiceSoftware.App;

public partial class SupplierPriceListImportWindow : Window
{
    private readonly ISupplierPriceListImportService importer;
    private PriceListWorkbookAnalysis? analysis;
    private PriceListImportPreview? preview;
    private bool applyingMapping;

    public SupplierPriceListImportWindow(
        ISupplierPriceListImportService importer,
        IEnumerable<Supplier> suppliers,
        string? initialFilePath = null)
    {
        InitializeComponent();
        this.importer = importer;
        SupplierCombo.ItemsSource = suppliers.Where(x => x.IsActive).OrderBy(x => x.CompanyName).ToList();
        SupplierCombo.SelectedIndex = SupplierCombo.Items.Count > 0 ? 0 : -1;
        FilePathText.Text = initialFilePath ?? "";
        DecimalSeparatorCombo.ItemsSource = Enum.GetValues<DecimalSeparatorMode>();
        DecimalSeparatorCombo.SelectedItem = DecimalSeparatorMode.Auto;
        ExistingItemsCombo.ItemsSource = Enum.GetValues<ExistingItemMode>();
        ExistingItemsCombo.SelectedItem = ExistingItemMode.CreateOnly;
    }

    public PriceListImportResult? Result { get; private set; }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel workbooks (*.xlsx;*.xls)|*.xlsx;*.xls" };
        if (dialog.ShowDialog(this) == true)
        {
            FilePathText.Text = dialog.FileName;
            analysis = null;
            preview = null;
            CandidateCombo.ItemsSource = null;
            RecordsGrid.ItemsSource = null;
            IssuesGrid.ItemsSource = null;
            SummaryText.Text = "File selected. Click Detect.";
        }
    }

    private async void Detect_Click(object sender, RoutedEventArgs e) => await DetectAsync();

    private async Task DetectAsync()
    {
        if (SupplierCombo.SelectedItem is not Supplier supplier)
        {
            MessageBox.Show(this, "Create or select a supplier first.", "Supplier required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(FilePathText.Text))
        {
            MessageBox.Show(this, "Select an Excel file first.", "File required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            IsEnabled = false;
            analysis = await importer.AnalyzeAsync(FilePathText.Text, supplier.Id);
            CandidateCombo.ItemsSource = analysis.Candidates;
            DetectionMessage.Text = analysis.SavedMappingMessage ?? $"Detected {analysis.Candidates.Count} possible table(s). Confirm the sheet, header row, and mapping.";
            if (analysis.SavedMapping is not null)
            {
                CandidateCombo.SelectedItem = analysis.Candidates.FirstOrDefault(x =>
                    string.Equals(x.SheetName, analysis.SavedMapping.SheetName, StringComparison.OrdinalIgnoreCase)
                    && x.HeaderRowNumber == analysis.SavedMapping.HeaderRowNumber)
                    ?? analysis.Candidates.FirstOrDefault(x => string.Equals(x.SheetName, analysis.SavedMapping.SheetName, StringComparison.OrdinalIgnoreCase));
                ApplyMapping(analysis.SavedMapping);
            }
            else CandidateCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Workbook detection failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsEnabled = true; }
    }

    private void CandidateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (applyingMapping || CandidateCombo.SelectedItem is not PriceListTableCandidate candidate) return;
        var choices = new List<MappingColumnChoice> { new(null, "Not mapped", "") };
        choices.AddRange(candidate.Columns.Select(x => new MappingColumnChoice(x.ColumnIndex, x.Header, x.Samples)));
        foreach (var combo in MappingCombos())
        {
            combo.ItemsSource = choices;
            combo.SelectedIndex = 0;
        }
        foreach (var suggestion in candidate.SuggestedMappings)
            SelectColumn(ComboFor(suggestion.Key), suggestion.Value);
        DetectionMessage.Text = candidate.RequiresConfirmation
            ? "Detection needs confirmation. Required fields or multiple price columns were found; choose the correct columns before previewing."
            : "Mapping suggested from the detected headers. Review it, then generate the preview.";
        preview = null;
        RecordsGrid.ItemsSource = null;
        IssuesGrid.ItemsSource = null;
        SummaryText.Text = "No preview generated.";
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (SupplierCombo.SelectedItem is not Supplier supplier || CandidateCombo.SelectedItem is not PriceListTableCandidate candidate) return;
        try
        {
            IsEnabled = false;
            var mapping = BuildMapping(candidate);
            preview = await importer.BuildPreviewAsync(FilePathText.Text, supplier.Id, mapping);
            RecordsGrid.ItemsSource = preview.Records;
            IssuesGrid.ItemsSource = preview.Issues;
            SummaryText.Text = $"Total {preview.TotalRecords}   |   Valid {preview.ValidRecords}   |   Create {preview.CreateCount}   |   Update {preview.UpdateCount}   |   Warnings {preview.WarningCount}   |   Errors {preview.ErrorCount}";
            if (preview.ErrorCount > 0) PreviewTabs.SelectedIndex = 1;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Preview failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsEnabled = true; }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (preview is null)
        {
            MessageBox.Show(this, "Generate and review the preview first.", "Preview required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var message = $"Import {preview.ValidRecords} valid record(s) for {preview.SupplierName}?\n\nCreate: {preview.CreateCount}\nUpdate: {preview.UpdateCount}\nErrors: {preview.ErrorCount}";
        if (MessageBox.Show(this, message, "Confirm supplier price-list import", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            IsEnabled = false;
            Result = await importer.ImportAsync(preview, ImportValidOnlyCheck.IsChecked == true);
            MessageBox.Show(this, $"Import completed.\n\nCreated: {Result.Created}\nUpdated: {Result.Updated}\nSkipped: {Result.Skipped}\n\nStore / Inventory was not changed.", "Import complete", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No partial changes were kept.\n\n{ex.Message}", "Import failed and rolled back", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsEnabled = true; }
    }

    private async void ResetMapping_Click(object sender, RoutedEventArgs e)
    {
        if (SupplierCombo.SelectedItem is not Supplier supplier) return;
        if (MessageBox.Show(this, $"Reset the saved Excel mapping for {supplier.CompanyName}?", "Reset mapping", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await importer.ResetSavedMappingAsync(supplier.Id);
        DetectionMessage.Text = "Saved mapping reset. Detect the file and confirm a new mapping.";
    }

    private void SupplierCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        analysis = null;
        preview = null;
        CandidateCombo.ItemsSource = null;
        RecordsGrid.ItemsSource = null;
        IssuesGrid.ItemsSource = null;
        if (IsLoaded) DetectionMessage.Text = "Supplier changed. Click Detect to validate or load its saved mapping.";
    }

    private PriceListImportMapping BuildMapping(PriceListTableCandidate candidate)
    {
        var columns = new Dictionary<PriceListImportField, int>();
        Add(columns, PriceListImportField.ItemNumber, ItemNumberCombo);
        Add(columns, PriceListImportField.Description, DescriptionCombo);
        Add(columns, PriceListImportField.ProductName, ProductNameCombo);
        Add(columns, PriceListImportField.Price, PriceCombo);
        Add(columns, PriceListImportField.Currency, CurrencyCombo);
        Add(columns, PriceListImportField.Category, CategoryCombo);
        Add(columns, PriceListImportField.Brand, BrandCombo);
        Add(columns, PriceListImportField.Unit, UnitCombo);
        Add(columns, PriceListImportField.TaxRate, TaxRateCombo);
        return new PriceListImportMapping
        {
            SheetName = candidate.SheetName,
            HeaderRowNumber = candidate.HeaderRowNumber,
            Columns = columns,
            DecimalSeparator = DecimalSeparatorCombo.SelectedItem is DecimalSeparatorMode separator ? separator : DecimalSeparatorMode.Auto,
            ExistingItems = ExistingItemsCombo.SelectedItem is ExistingItemMode existing ? existing : ExistingItemMode.CreateOnly
        };
    }

    private void ApplyMapping(PriceListImportMapping mapping)
    {
        if (CandidateCombo.SelectedItem is not PriceListTableCandidate candidate) return;
        applyingMapping = true;
        try
        {
            var choices = new List<MappingColumnChoice> { new(null, "Not mapped", "") };
            choices.AddRange(candidate.Columns.Select(x => new MappingColumnChoice(x.ColumnIndex, x.Header, x.Samples)));
            foreach (var combo in MappingCombos()) { combo.ItemsSource = choices; combo.SelectedIndex = 0; }
            foreach (var entry in mapping.Columns) SelectColumn(ComboFor(entry.Key), entry.Value);
            DecimalSeparatorCombo.SelectedItem = mapping.DecimalSeparator;
            ExistingItemsCombo.SelectedItem = mapping.ExistingItems;
        }
        finally { applyingMapping = false; }
    }

    private IEnumerable<ComboBox> MappingCombos() =>
        [ItemNumberCombo, DescriptionCombo, ProductNameCombo, PriceCombo, CurrencyCombo, CategoryCombo, BrandCombo, UnitCombo, TaxRateCombo];

    private ComboBox ComboFor(PriceListImportField field) => field switch
    {
        PriceListImportField.ItemNumber => ItemNumberCombo,
        PriceListImportField.ProductName => ProductNameCombo,
        PriceListImportField.Description => DescriptionCombo,
        PriceListImportField.Price => PriceCombo,
        PriceListImportField.Currency => CurrencyCombo,
        PriceListImportField.Category => CategoryCombo,
        PriceListImportField.Brand => BrandCombo,
        PriceListImportField.Unit => UnitCombo,
        PriceListImportField.TaxRate => TaxRateCombo,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private static void SelectColumn(ComboBox combo, int columnIndex)
        => combo.SelectedItem = combo.Items.Cast<MappingColumnChoice>().FirstOrDefault(x => x.ColumnIndex == columnIndex);

    private static void Add(IDictionary<PriceListImportField, int> target, PriceListImportField field, ComboBox combo)
    {
        if (combo.SelectedItem is MappingColumnChoice { ColumnIndex: int column }) target[field] = column;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed record MappingColumnChoice(int? ColumnIndex, string Header, string Samples)
    {
        public override string ToString() => string.IsNullOrWhiteSpace(Samples) ? Header : $"{Header}  |  {Samples}";
    }
}
