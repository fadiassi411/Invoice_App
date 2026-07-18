using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;
using InvoiceSoftware.Reporting;
using InvoiceSoftware.Services;
using Microsoft.Win32;

namespace InvoiceSoftware.App.ViewModels;

/// <summary>Presentation logic for quotation entry, history, search, document output, and conversion.</summary>
public sealed class QuotationWorkspaceViewModel : ObservableObject
{
    private readonly IQuotationService _service;
    private readonly ILookupService _lookup;
    private readonly IQuotationPdfService _pdf;
    private readonly QuotationCalculator _calculator;
    private Quotation? _currentQuotation;
    private Quotation? _selectedQuotation;
    private QuotationItem? _selectedLine;
    private Customer? _selectedCustomer;
    private CompanySettings _company = new();
    private QuotationStatistics? _statistics;
    private string _statusMessage = "Quotation workspace ready.";
    private string _searchText = "";
    private Customer? _filterCustomer;
    private DateTime? _filterFrom;
    private DateTime? _filterTo;
    private string _filterStatus = "All";
    private string _filterValidity = "All";
    private string _stockWarning = "";
    private bool _hasUnsavedChanges;

    public QuotationWorkspaceViewModel(IQuotationService service, ILookupService lookup, IQuotationPdfService pdf, QuotationCalculator calculator)
    {
        _service = service;
        _lookup = lookup;
        _pdf = pdf;
        _calculator = calculator;
        Quotations = [];
        Items = [];
        Customers = [];
        Products = [];

        NewCommand = new RelayCommand(NewAsync);
        SaveCommand = new RelayCommand(SaveAsync);
        SaveAndPreviewCommand = new RelayCommand(SaveAndPreviewAsync);
        RecalculateCommand = new RelayCommand(RecalculateAsync);
        PreviewCommand = new RelayCommand(PreviewAsync);
        ExportPdfCommand = new RelayCommand(ExportPdfAsync);
        PrintCommand = new RelayCommand(PrintAsync);
        SearchCommand = new RelayCommand(SearchAsync);
        EditCommand = new RelayCommand<Quotation>(EditAsync);
        DuplicateCommand = new RelayCommand<Quotation>(DuplicateAsync);
        CreateRevisionCommand = new RelayCommand<Quotation>(CreateRevisionAsync);
        ArchiveCommand = new RelayCommand<Quotation>(ArchiveAsync);
        AddStockItemCommand = new RelayCommand(AddStockItemAsync);
        AddManualItemCommand = new RelayCommand(AddManualItemAsync);
        AddServiceCommand = new RelayCommand(AddServiceAsync);
        AddSectionCommand = new RelayCommand(AddSectionAsync);
        DuplicateLineCommand = new RelayCommand<QuotationItem>(DuplicateLineAsync);
        DeleteLineCommand = new RelayCommand<QuotationItem>(DeleteLineAsync);
        MoveLineUpCommand = new RelayCommand<QuotationItem>(MoveLineUpAsync);
        MoveLineDownCommand = new RelayCommand<QuotationItem>(MoveLineDownAsync);
        ApplyCustomerCommand = new RelayCommand(ApplyCustomerAsync);
        ChangeStatusCommand = new RelayCommand<QuotationStatus>(ChangeStatusAsync);
        ConvertToInvoiceCommand = new RelayCommand<Quotation>(ConvertToInvoiceAsync);
        ExportCsvCommand = new RelayCommand(ExportCsvAsync);
        CloseEditorCommand = new RelayCommand(CloseEditorAsync);
    }

    public ObservableCollection<Quotation> Quotations { get; }
    public ObservableCollection<QuotationItem> Items { get; }
    public ObservableCollection<Customer> Customers { get; }
    public ObservableCollection<Product> Products { get; }
    public IReadOnlyList<string> StatusFilters { get; } = ["All", .. Enum.GetNames<QuotationStatus>()];
    public IReadOnlyList<string> ValidityFilters { get; } = ["All", "Valid", "Expired"];
    public IReadOnlyList<QuotationStatus> QuotationStatuses { get; } = Enum.GetValues<QuotationStatus>();
    public IReadOnlyList<TaxMode> TaxModes { get; } = Enum.GetValues<TaxMode>();
    public IReadOnlyList<DiscountType> DiscountTypes { get; } = Enum.GetValues<DiscountType>();

    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAndPreviewCommand { get; }
    public ICommand RecalculateCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand CreateRevisionCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand AddStockItemCommand { get; }
    public ICommand AddManualItemCommand { get; }
    public ICommand AddServiceCommand { get; }
    public ICommand AddSectionCommand { get; }
    public ICommand DuplicateLineCommand { get; }
    public ICommand DeleteLineCommand { get; }
    public ICommand MoveLineUpCommand { get; }
    public ICommand MoveLineDownCommand { get; }
    public ICommand ApplyCustomerCommand { get; }
    public ICommand ChangeStatusCommand { get; }
    public ICommand ConvertToInvoiceCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand CloseEditorCommand { get; }

    public Action? OpenEditorRequested { get; set; }
    public Action<Invoice>? OpenInvoiceRequested { get; set; }
    public Action? CloseEditorRequested { get; set; }

    public Quotation? CurrentQuotation
    {
        get => _currentQuotation;
        private set
        {
            if (!SetProperty(ref _currentQuotation, value)) return;
            Items.Clear();
            if (value is not null) foreach (var item in value.Items.OrderBy(x => x.DisplayOrder)) Items.Add(item);
            SelectedCustomer = value is null ? null : Customers.FirstOrDefault(x => x.Id == value.CustomerId);
            Raise(nameof(CanEditCurrent));
        }
    }
    public Quotation? SelectedQuotation { get => _selectedQuotation; set => SetProperty(ref _selectedQuotation, value); }
    public QuotationItem? SelectedLine { get => _selectedLine; set => SetProperty(ref _selectedLine, value); }
    public Customer? SelectedCustomer { get => _selectedCustomer; set => SetProperty(ref _selectedCustomer, value); }
    public CompanySettings Company { get => _company; private set => SetProperty(ref _company, value); }
    public QuotationStatistics? Statistics { get => _statistics; private set => SetProperty(ref _statistics, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public Customer? FilterCustomer { get => _filterCustomer; set => SetProperty(ref _filterCustomer, value); }
    public DateTime? FilterFrom { get => _filterFrom; set => SetProperty(ref _filterFrom, value); }
    public DateTime? FilterTo { get => _filterTo; set => SetProperty(ref _filterTo, value); }
    public string FilterStatus { get => _filterStatus; set => SetProperty(ref _filterStatus, value); }
    public string FilterValidity { get => _filterValidity; set => SetProperty(ref _filterValidity, value); }
    public string StockWarning { get => _stockWarning; private set => SetProperty(ref _stockWarning, value); }
    public bool HasUnsavedChanges { get => _hasUnsavedChanges; private set => SetProperty(ref _hasUnsavedChanges, value); }
    public bool CanEditCurrent => CurrentQuotation?.Status is QuotationStatus.Draft or QuotationStatus.Sent or QuotationStatus.UnderReview or QuotationStatus.Rejected;

    public async Task InitializeAsync()
    {
        Company = await _lookup.GetCompanySettingsAsync();
        await RefreshLookupsAsync();
        await SearchAsync();
    }

    public Task StartNewAsync() => NewAsync();

    public async Task ApplySearchAsync(string? searchText)
    {
        SearchText = searchText?.Trim() ?? "";
        await SearchAsync();
    }

    public async Task RefreshLookupsAsync(int? preferredCustomerId = null)
    {
        var customerId = preferredCustomerId ?? SelectedCustomer?.Id ?? CurrentQuotation?.CustomerId;
        await Replace(Customers, await _lookup.GetCustomersAsync());
        await Replace(Products, await _lookup.GetProductsAsync());
        SelectedCustomer = Customers.FirstOrDefault(x => x.Id == customerId) ?? Customers.FirstOrDefault();
    }

    public void NotifyEditorChanged()
    {
        HasUnsavedChanges = true;
        Recalculate();
    }

    public void MarkUnsaved() => HasUnsavedChanges = true;

    public void Recalculate()
    {
        if (CurrentQuotation is null || Items.Count == 0) return;
        CurrentQuotation.Items = Items.ToList();
        try
        {
            _calculator.Calculate(CurrentQuotation);
            StockWarning = BuildStockWarning();
            Raise(nameof(CurrentQuotation));
            RefreshItems();
        }
        catch (InvalidOperationException ex) { StatusMessage = ex.Message; }
    }

    private async Task NewAsync()
    {
        if (!ConfirmDiscard()) return;
        await RefreshLookupsAsync(SelectedCustomer?.Id);
        SelectedCustomer ??= Customers.FirstOrDefault();
        if (SelectedCustomer is null) throw new InvalidOperationException("Create a customer before creating a quotation.");
        CurrentQuotation = await _service.CreateDraftAsync(SelectedCustomer.Id);
        HasUnsavedChanges = true;
        StockWarning = "";
        StatusMessage = $"New quotation {CurrentQuotation.DisplayNumber} is ready.";
        OpenEditorRequested?.Invoke();
    }

    private async Task SaveAsync()
    {
        if (CurrentQuotation is null) throw new InvalidOperationException("Create or open a quotation first.");
        CurrentQuotation.Items = Items.ToList();
        CurrentQuotation = await _service.SaveAsync(CurrentQuotation);
        HasUnsavedChanges = false;
        await SearchAsync();
        StatusMessage = $"Quotation {CurrentQuotation.DisplayNumber} saved.";
    }

    private async Task SaveAndPreviewAsync() { await SaveAsync(); await PreviewAsync(); }
    private Task RecalculateAsync() { HasUnsavedChanges = true; Recalculate(); return Task.CompletedTask; }

    private async Task PreviewAsync()
    {
        var quotation = RequireCurrent();
        Recalculate();
        var file = Path.Combine(Path.GetTempPath(), $"{SafeFileName(quotation.DisplayNumber)}-preview.pdf");
        _pdf.ExportQuotation(quotation, Company, file);
        Open(file);
        StatusMessage = $"Preview opened for {quotation.DisplayNumber}.";
        await Task.CompletedTask;
    }

    private async Task ExportPdfAsync()
    {
        var quotation = RequireCurrent();
        Recalculate();
        var customer = SafeFileName(quotation.CustomerCompanySnapshot ?? quotation.CustomerNameSnapshot);
        var dialog = new SaveFileDialog
        {
            Filter = "PDF document (*.pdf)|*.pdf",
            FileName = $"Quotation_{SafeFileName(quotation.QuotationNumber)}_{customer}.pdf",
            InitialDirectory = Directory.Exists(Company.DefaultQuotationPdfFolder) ? Company.DefaultQuotationPdfFolder : null
        };
        if (dialog.ShowDialog() != true) return;
        _pdf.ExportQuotation(quotation, Company, dialog.FileName);
        if (Company.OpenQuotationPdfAfterExport) Open(dialog.FileName);
        StatusMessage = $"Quotation PDF exported to {dialog.FileName}.";
        await Task.CompletedTask;
    }

    private async Task PrintAsync()
    {
        var quotation = RequireCurrent();
        Recalculate();
        var file = Path.Combine(Path.GetTempPath(), $"{SafeFileName(quotation.DisplayNumber)}-print.pdf");
        _pdf.ExportQuotation(quotation, Company, file);
        Open(file);
        StatusMessage = "The quotation opened in the PDF viewer. Select a printer or press Ctrl+P.";
        await Task.CompletedTask;
    }

    private async Task SearchAsync()
    {
        QuotationStatus? status = Enum.TryParse<QuotationStatus>(FilterStatus, out var parsed) ? parsed : null;
        var validity = Enum.TryParse<QuotationValidityFilter>(FilterValidity, out var parsedValidity) ? parsedValidity : QuotationValidityFilter.All;
        await Replace(Quotations, await _service.SearchAsync(new QuotationSearchCriteria(SearchText, FilterCustomer?.Id, FilterFrom, FilterTo, status, validity)));
        Statistics = await _service.GetStatisticsAsync();
        StatusMessage = $"{Quotations.Count} quotation(s) found.";
    }

    private async Task EditAsync(Quotation? quotation)
    {
        quotation ??= SelectedQuotation;
        if (quotation is null || !ConfirmDiscard()) return;
        CurrentQuotation = await _service.GetAsync(quotation.Id) ?? throw new InvalidOperationException("Quotation was not found.");
        HasUnsavedChanges = false;
        StatusMessage = $"Opened {CurrentQuotation.DisplayNumber}.";
        OpenEditorRequested?.Invoke();
    }

    private async Task DuplicateAsync(Quotation? quotation)
    {
        quotation ??= SelectedQuotation;
        if (quotation is null) return;
        CurrentQuotation = await _service.DuplicateAsync(quotation.Id);
        HasUnsavedChanges = false;
        await SearchAsync();
        StatusMessage = $"Duplicated as {CurrentQuotation.DisplayNumber}.";
        OpenEditorRequested?.Invoke();
    }

    private async Task CreateRevisionAsync(Quotation? quotation)
    {
        quotation ??= SelectedQuotation ?? CurrentQuotation;
        if (quotation is null || quotation.Id == 0) throw new InvalidOperationException("Save the quotation before creating a revision.");
        CurrentQuotation = await _service.CreateRevisionAsync(quotation.Id);
        HasUnsavedChanges = false;
        await SearchAsync();
        StatusMessage = $"Created revision {CurrentQuotation.DisplayNumber}.";
        OpenEditorRequested?.Invoke();
    }

    private async Task ArchiveAsync(Quotation? quotation)
    {
        quotation ??= SelectedQuotation;
        if (quotation is null) return;
        if (MessageBox.Show($"Archive {quotation.DisplayNumber}? Its audit history and number will be preserved.", "Archive quotation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await _service.ArchiveAsync(quotation.Id);
        if (CurrentQuotation?.Id == quotation.Id) CurrentQuotation = null;
        await SearchAsync();
    }

    private async Task AddStockItemAsync()
    {
        EnsureCurrent();
        await RefreshLookupsAsync(SelectedCustomer?.Id);
        var dialog = new ProductSelectionDialog(Products.Where(x => x.IsActive).ToList(), "Quotation") { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.SelectedProduct is null) return;
        var product = dialog.SelectedProduct;
        AddLine(new QuotationItem
        {
            ItemType = product.Type == ProductType.Service ? QuotationItemType.Service : QuotationItemType.StockItem,
            StockItemId = product.Id, ItemReferenceSnapshot = product.Code, PartNumberSnapshot = product.PartNumber,
            BarcodeSnapshot = product.Barcode, DescriptionSnapshot = product.Description, UnitSnapshot = product.Unit,
            BrandSnapshot = product.Brand, ManufacturerSnapshot = product.Manufacturer, WarrantySnapshot = product.Warranty,
            AvailableStockSnapshot = product.CurrentQuantity, Quantity = 1, UnitPrice = product.SellingPrice,
            TaxPercentage = product.TaxPercentage, DiscountType = DiscountType.Percentage, DiscountValue = product.DefaultDiscount
        });
    }

    private Task AddManualItemAsync() { EnsureCurrent(); AddLine(new QuotationItem { ItemType = QuotationItemType.ManualItem, DescriptionSnapshot = "Custom item", UnitSnapshot = "ea", Quantity = 1, TaxPercentage = Company.DefaultTaxPercentage }); return Task.CompletedTask; }
    private Task AddServiceAsync() { EnsureCurrent(); AddLine(new QuotationItem { ItemType = QuotationItemType.Service, DescriptionSnapshot = "Service / labour", UnitSnapshot = "hr", Quantity = 1, TaxPercentage = Company.DefaultTaxPercentage }); return Task.CompletedTask; }
    private Task AddSectionAsync() { EnsureCurrent(); AddLine(new QuotationItem { ItemType = QuotationItemType.SectionHeader, DescriptionSnapshot = "Section heading", Quantity = 0, UnitSnapshot = "" }); return Task.CompletedTask; }

    private Task DuplicateLineAsync(QuotationItem? source)
    {
        source ??= SelectedLine;
        if (source is null) return Task.CompletedTask;
        AddLine(new QuotationItem
        {
            ItemType = source.ItemType, StockItemId = source.StockItemId, ItemReferenceSnapshot = source.ItemReferenceSnapshot,
            PartNumberSnapshot = source.PartNumberSnapshot, BarcodeSnapshot = source.BarcodeSnapshot, DescriptionSnapshot = source.DescriptionSnapshot,
            UnitSnapshot = source.UnitSnapshot, BrandSnapshot = source.BrandSnapshot, ManufacturerSnapshot = source.ManufacturerSnapshot,
            WarrantySnapshot = source.WarrantySnapshot, AvailableStockSnapshot = source.AvailableStockSnapshot, Quantity = source.Quantity,
            UnitPrice = source.UnitPrice, DiscountType = source.DiscountType, DiscountValue = source.DiscountValue,
            DiscountPercentage = source.DiscountPercentage, TaxPercentage = source.TaxPercentage, Notes = source.Notes
        });
        return Task.CompletedTask;
    }

    private Task DeleteLineAsync(QuotationItem? item) { item ??= SelectedLine; if (item is not null) { Items.Remove(item); NormalizeLines(); Changed(); } return Task.CompletedTask; }
    private Task MoveLineUpAsync(QuotationItem? item) { Move(item ?? SelectedLine, -1); return Task.CompletedTask; }
    private Task MoveLineDownAsync(QuotationItem? item) { Move(item ?? SelectedLine, 1); return Task.CompletedTask; }

    private Task ApplyCustomerAsync()
    {
        var quotation = RequireCurrent();
        var customer = SelectedCustomer ?? throw new InvalidOperationException("Select a customer.");
        quotation.CustomerId = customer.Id; quotation.Customer = null; quotation.CustomerNameSnapshot = customer.Name;
        quotation.CustomerCompanySnapshot = customer.CompanyName ?? customer.Name; quotation.BillingAddressSnapshot = customer.Address;
        quotation.ProjectAddressSnapshot = customer.DeliveryAddress; quotation.ContactPersonSnapshot = customer.AttentionName ?? customer.ContactPerson;
        quotation.TelephoneSnapshot = customer.Telephone; quotation.MobileSnapshot = customer.Mobile; quotation.EmailSnapshot = customer.Email;
        quotation.TaxNumberSnapshot = customer.TaxNumber; Changed(); StatusMessage = "Customer snapshot updated.";
        return Task.CompletedTask;
    }

    private async Task ChangeStatusAsync(QuotationStatus status)
    {
        var quotation = RequireCurrent();
        if (quotation.Id == 0) await SaveAsync();
        await _service.ChangeStatusAsync(quotation.Id, status);
        CurrentQuotation = await _service.GetAsync(quotation.Id);
        HasUnsavedChanges = false;
        await SearchAsync();
        StatusMessage = $"Quotation marked {status}.";
    }

    private async Task ConvertToInvoiceAsync(Quotation? quotation)
    {
        quotation ??= SelectedQuotation ?? CurrentQuotation;
        if (quotation is null) return;
        if (MessageBox.Show($"Convert {quotation.DisplayNumber} to a new draft invoice? This operation cannot be repeated.", "Convert to invoice", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var invoice = await _service.ConvertToInvoiceAsync(quotation.Id);
        await SearchAsync();
        CurrentQuotation = await _service.GetAsync(quotation.Id);
        HasUnsavedChanges = false;
        StatusMessage = $"Created invoice {invoice.ReferenceNumber}.";
        if (MessageBox.Show($"Invoice {invoice.ReferenceNumber} was created. Open it now?", "Conversion complete", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            OpenInvoiceRequested?.Invoke(invoice);
    }

    private async Task ExportCsvAsync()
    {
        var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"quotations-{DateTime.Today:yyyyMMdd}.csv" };
        if (dialog.ShowDialog() != true) return;
        var rows = new List<string> { "QuotationNumber,Revision,Date,Customer,Project,Subject,Subtotal,Discount,Tax,Total,Currency,ValidUntil,Status,CreatedBy,LastModified" };
        rows.AddRange(Quotations.Select(x => string.Join(',', new[] { x.QuotationNumber, x.RevisionNumber.ToString(), x.QuotationDate.ToString("yyyy-MM-dd"), x.CustomerNameSnapshot, x.ProjectName, x.Subject, x.Subtotal.ToString(CultureInfo.InvariantCulture), x.DiscountTotal.ToString(CultureInfo.InvariantCulture), x.TaxTotal.ToString(CultureInfo.InvariantCulture), x.GrandTotal.ToString(CultureInfo.InvariantCulture), x.CurrencyCode, x.ValidUntil.ToString("yyyy-MM-dd"), x.Status.ToString(), x.CreatedBy, x.ModifiedAt?.ToString("O") }.Select(Csv))));
        await File.WriteAllLinesAsync(dialog.FileName, rows, Encoding.UTF8);
        StatusMessage = $"Quotation list exported to {dialog.FileName}.";
    }

    private Task CloseEditorAsync() { if (ConfirmDiscard()) CloseEditorRequested?.Invoke(); return Task.CompletedTask; }

    private void AddLine(QuotationItem item) { item.DisplayOrder = Items.Count + 1; item.LineNumber = item.DisplayOrder; Items.Add(item); SelectedLine = item; Changed(); }
    private void Move(QuotationItem? item, int direction) { if (item is null) return; var index = Items.IndexOf(item); var target = index + direction; if (target < 0 || target >= Items.Count) return; Items.Move(index, target); NormalizeLines(); Changed(); }
    private void NormalizeLines() { for (var index = 0; index < Items.Count; index++) { Items[index].DisplayOrder = index + 1; Items[index].LineNumber = index + 1; } }
    private void Changed() { HasUnsavedChanges = true; Recalculate(); }
    private void RefreshItems() { var selected = SelectedLine; var rows = Items.ToList(); Items.Clear(); foreach (var row in rows) Items.Add(row); SelectedLine = selected; }
    private void EnsureCurrent() { if (CurrentQuotation is null) throw new InvalidOperationException("Create or open a quotation first."); }
    private Quotation RequireCurrent() { EnsureCurrent(); return CurrentQuotation!; }
    private bool ConfirmDiscard() => !HasUnsavedChanges || MessageBox.Show("This quotation has unsaved changes. Discard them?", "Unsaved quotation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private string BuildStockWarning()
    {
        var exceeded = Items.Where(x => x.StockItemId is not null && x.ItemType == QuotationItemType.StockItem && x.Quantity > x.AvailableStockSnapshot).ToList();
        return exceeded.Count == 0 ? "" : $"Warning: {exceeded.Count} line(s) exceed available stock. Saving does not deduct stock.";
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c)).Trim('_');
    }
    private static void Open(string file) => Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    private static string Csv(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
    private static Task Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); return Task.CompletedTask; }
}
