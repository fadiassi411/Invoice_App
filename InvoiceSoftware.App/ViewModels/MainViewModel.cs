using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text;
using InvoiceSoftware.App;
using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using InvoiceSoftware.Licensing;
using InvoiceSoftware.Reporting;
using InvoiceSoftware.Services;
using Microsoft.Win32;

namespace InvoiceSoftware.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ILookupService _lookup;
    private readonly IInvoiceService _invoices;
    private readonly IReceiptService _receipts;
    private readonly IDashboardService _dashboard;
    private readonly IBackupService _backup;
    private readonly IInventoryService _inventory;
    private readonly IInvoicePdfService _pdf;
    private readonly IInvoiceLicenseService _licenseService;

    private string _selectedSection = "Dashboard";
    private string _statusMessage = "Ready";
    private string? _searchText;
    private CompanySettings _company = new();
    private Customer? _selectedCustomer;
    private Product? _selectedProduct;
    private Invoice? _selectedInvoice;
    private DashboardSnapshot? _snapshot;
    private Product? _inventoryProduct;
    private Category? _selectedCategoryFilter;
    private Supplier? _selectedSupplierFilter;
    private string _inventorySearchText = "";
    private string _inventoryActiveFilter = "All";
    private bool _lowStockOnly;
    private decimal _inventoryQuantityInput;
    private decimal _inventorySellingPercentage;
    private bool _updatingInventoryPricing;
    private string _stockAdjustmentNotes = "";
    private Category? _selectedCategory;
    private Supplier? _selectedSupplier;
    private DateTime? _reportFrom = DateTime.Today.AddMonths(-1);
    private DateTime? _reportTo = DateTime.Today;
    private InvoiceLicenseStatus _licenseStatus;

    public MainViewModel(ILookupService lookup, IInvoiceService invoices, IReceiptService receipts, IDashboardService dashboard, IBackupService backup, IInvoicePdfService pdf, IInventoryService inventory, QuotationWorkspaceViewModel quotations, IInvoiceLicenseService licenseService)
    {
        _lookup = lookup;
        _invoices = invoices;
        _receipts = receipts;
        _dashboard = dashboard;
        _backup = backup;
        _pdf = pdf;
        _inventory = inventory;
        _licenseService = licenseService;
        _licenseStatus = licenseService.GetStatus();
        Quotations = quotations;
        Quotations.OpenEditorRequested = () => SelectedSection = "New Quotation";
        Quotations.CloseEditorRequested = () => SelectedSection = "Quotations";
        Quotations.OpenInvoiceRequested = invoice => { SelectedInvoice = invoice; SelectedSection = "New Invoice"; };

        Sections = ["Dashboard", "New Invoice", "Invoices", "New Quotation", "Quotations", "Customers", "Store / Inventory", "Inventory Reports", "Products and Services", "Receipts", "Reports", "Backup and Restore", "Company Settings", "Application Settings"];
        Customers = [];
        Products = [];
        InvoiceItems = [];
        Invoices = [];
        Receipts = [];
        InventoryProducts = [];
        Categories = [];
        Suppliers = [];
        StockMovements = [];
        CurrentStockReport = [];
        SalesProfitReport = [];
        LowStockReport = [];

        NavigateCommand = new RelayCommand<string>(NavigateAsync);
        RefreshCommand = new RelayCommand(RefreshAsync);
        SearchCommand = new RelayCommand(SearchCurrentSectionAsync);
        NewInvoiceCommand = new RelayCommand(CreateInvoiceAsync);
        AddInvoiceRowCommand = new RelayCommand(AddInvoiceRowAsync);
        SelectProductCommand = new RelayCommand(SelectProductAsync);
        RemoveInvoiceRowCommand = new RelayCommand<InvoiceItem>(RemoveInvoiceRowAsync);
        SaveInvoiceCommand = new RelayCommand(SaveInvoiceAsync);
        EditInvoiceCommand = new RelayCommand<Invoice>(EditInvoiceAsync, invoice => invoice is not null);
        DeleteInvoiceCommand = new RelayCommand<Invoice>(DeleteInvoiceAsync, invoice => invoice is not null);
        MarkPaidCommand = new RelayCommand<Invoice>(MarkPaidAsync, invoice => invoice is not null && invoice.PaymentStatus is not PaymentStatus.Paid and not PaymentStatus.Overpaid);
        ExportInvoicePdfCommand = new RelayCommand<Invoice>(ExportInvoicePdfAsync);
        PrintInvoiceCommand = new RelayCommand<Invoice>(PrintInvoiceAsync);
        CreateReceiptCommand = new RelayCommand<Invoice>(CreateReceiptAsync, CanCreateReceipt);
        ExportReceiptForInvoiceCommand = new RelayCommand<Invoice>(ExportReceiptForInvoiceAsync, invoice => invoice?.Receipts.Count > 0);
        ExportReceiptPdfCommand = new RelayCommand<Receipt>(ExportReceiptPdfAsync);
        PrintReceiptCommand = new RelayCommand<Receipt>(PrintReceiptAsync);
        EditReceiptCommand = new RelayCommand<Receipt>(EditReceiptAsync, receipt => receipt is not null);
        DeleteReceiptCommand = new RelayCommand<Receipt>(DeleteReceiptAsync, receipt => receipt is not null);
        AddCustomerCommand = new RelayCommand(AddCustomerAsync);
        SaveCustomerCommand = new RelayCommand(SaveCustomerAsync);
        DeleteCustomerCommand = new RelayCommand(DeleteCustomerAsync);
        AddProductCommand = new RelayCommand(AddProductAsync);
        SaveCompanyCommand = new RelayCommand(SaveCompanyAsync);
        ChooseLogoCommand = new RelayCommand(ChooseLogoAsync);
        BackupCommand = new RelayCommand(BackupAsync);
        RestoreCommand = new RelayCommand(RestoreAsync);
        NewInventoryProductCommand = new RelayCommand(NewInventoryProductAsync);
        SaveInventoryProductCommand = new RelayCommand(SaveInventoryProductAsync);
        DeleteInventoryProductCommand = new RelayCommand(DeleteInventoryProductAsync);
        DuplicateInventoryProductCommand = new RelayCommand(DuplicateInventoryProductAsync);
        ClearInventoryProductCommand = new RelayCommand(NewInventoryProductAsync);
        SearchInventoryCommand = new RelayCommand(SearchInventoryAsync);
        ViewStockHistoryCommand = new RelayCommand(ViewStockHistoryAsync);
        ExportProductsCsvCommand = new RelayCommand(ExportProductsCsvAsync);
        ImportProductsCsvCommand = new RelayCommand(ImportProductsCsvAsync);
        PrintProductsCommand = new RelayCommand(PrintProductsAsync);
        NewCategoryCommand = new RelayCommand(NewCategoryAsync);
        SaveCategoryCommand = new RelayCommand(SaveCategoryAsync);
        NewSupplierCommand = new RelayCommand(NewSupplierAsync);
        SaveSupplierCommand = new RelayCommand(SaveSupplierAsync);
        RefreshReportsCommand = new RelayCommand(RefreshReportsAsync);
        ExportStockReportCommand = new RelayCommand(ExportStockReportAsync);
        ExportProfitReportCommand = new RelayCommand(ExportProfitReportAsync);
        ActivateLicenseCommand = new RelayCommand(ActivateLicenseAsync);
        _ = RefreshAsync();
    }

    public string[] Sections { get; }
    public QuotationWorkspaceViewModel Quotations { get; }
    public ObservableCollection<Customer> Customers { get; }
    public ObservableCollection<Product> Products { get; }
    public ObservableCollection<Invoice> Invoices { get; }
    public ObservableCollection<InvoiceItem> InvoiceItems { get; }
    public ObservableCollection<Receipt> Receipts { get; }
    public ObservableCollection<Product> InventoryProducts { get; }
    public ObservableCollection<Category> Categories { get; }
    public ObservableCollection<Supplier> Suppliers { get; }
    public ObservableCollection<StockMovement> StockMovements { get; }
    public ObservableCollection<CurrentStockReportRow> CurrentStockReport { get; }
    public ObservableCollection<SalesProfitReportRow> SalesProfitReport { get; }
    public ObservableCollection<CurrentStockReportRow> LowStockReport { get; }
    public string[] ActiveFilters { get; } = ["All", "Active", "Inactive"];

    public ICommand NavigateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand NewInvoiceCommand { get; }
    public ICommand AddInvoiceRowCommand { get; }
    public ICommand SelectProductCommand { get; }
    public ICommand RemoveInvoiceRowCommand { get; }
    public ICommand SaveInvoiceCommand { get; }
    public ICommand EditInvoiceCommand { get; }
    public ICommand DeleteInvoiceCommand { get; }
    public ICommand MarkPaidCommand { get; }
    public ICommand ExportInvoicePdfCommand { get; }
    public ICommand PrintInvoiceCommand { get; }
    public ICommand CreateReceiptCommand { get; }
    public ICommand ExportReceiptForInvoiceCommand { get; }
    public ICommand ExportReceiptPdfCommand { get; }
    public ICommand PrintReceiptCommand { get; }
    public ICommand EditReceiptCommand { get; }
    public ICommand DeleteReceiptCommand { get; }
    public ICommand AddCustomerCommand { get; }
    public ICommand SaveCustomerCommand { get; }
    public ICommand DeleteCustomerCommand { get; }
    public ICommand AddProductCommand { get; }
    public ICommand SaveCompanyCommand { get; }
    public ICommand ChooseLogoCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand NewInventoryProductCommand { get; }
    public ICommand SaveInventoryProductCommand { get; }
    public ICommand DeleteInventoryProductCommand { get; }
    public ICommand DuplicateInventoryProductCommand { get; }
    public ICommand ClearInventoryProductCommand { get; }
    public ICommand SearchInventoryCommand { get; }
    public ICommand ViewStockHistoryCommand { get; }
    public ICommand ExportProductsCsvCommand { get; }
    public ICommand ImportProductsCsvCommand { get; }
    public ICommand PrintProductsCommand { get; }
    public ICommand NewCategoryCommand { get; }
    public ICommand SaveCategoryCommand { get; }
    public ICommand NewSupplierCommand { get; }
    public ICommand SaveSupplierCommand { get; }
    public ICommand RefreshReportsCommand { get; }
    public ICommand ExportStockReportCommand { get; }
    public ICommand ExportProfitReportCommand { get; }
    public ICommand ActivateLicenseCommand { get; }

    public string ApplicationVersion => "1.2.2";
    public string InstallationId => _licenseService.InstallationId;
    public string LicenseStatus => _licenseStatus.IsLicensed ? "Licensed" : "Not licensed";
    public string LicenseCustomer => _licenseStatus.CustomerName ?? "-";
    public string LicenseEdition => _licenseStatus.Edition ?? "-";
    public string LicenseExpiry => _licenseStatus.ExpiresAtUtc is null ? (_licenseStatus.IsLicensed ? "Perpetual" : "-") : _licenseStatus.ExpiresAtUtc.Value.ToString("yyyy-MM-dd");
    public string LicenseMessage => _licenseStatus.Message;

    public string SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
                Raise(nameof(GlobalSearchHint));
        }
    }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string? SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public string GlobalSearchHint => SelectedSection switch
    {
        "Customers" => "Search customers by code, name, contact, address, phone, email, or tax number",
        "Products and Services" => "Search products and services by reference, part number, name, category, brand, supplier, or location",
        "Store / Inventory" => "Search inventory by reference, part number, barcode, product, category, brand, supplier, or location",
        "Inventory Reports" => "Search inventory reports by product, reference, location, or invoice",
        "Receipts" => "Search receipts by receipt number, invoice, customer, payment method, or transaction reference",
        "New Quotation" or "Quotations" => "Search quotations by number, customer, project, subject, or customer reference",
        _ => "Search invoices by reference or customer"
    };
    public CompanySettings Company { get => _company; set => SetProperty(ref _company, value); }
    public Customer? SelectedCustomer { get => _selectedCustomer; set => SetProperty(ref _selectedCustomer, value); }
    public Product? SelectedProduct { get => _selectedProduct; set => SetProperty(ref _selectedProduct, value); }
    public Invoice? SelectedInvoice
    {
        get => _selectedInvoice;
        set
        {
            if (SetProperty(ref _selectedInvoice, value))
            {
                InvoiceItems.Clear();
                if (value is not null) foreach (var item in value.Items.OrderBy(x => x.SortOrder)) InvoiceItems.Add(item);
            }
        }
    }
    public DashboardSnapshot? Snapshot { get => _snapshot; set => SetProperty(ref _snapshot, value); }
    public Product? InventoryProduct
    {
        get => _inventoryProduct;
        set
        {
            if (SetProperty(ref _inventoryProduct, value))
            {
                InventoryQuantityInput = value?.CurrentQuantity ?? 0;
                UpdateInventorySellingPercentageFromPrices();
                Raise(nameof(InventoryCostPrice));
                Raise(nameof(InventorySellingPrice));
                StockAdjustmentNotes = "";
            }
        }
    }
    public Category? SelectedCategoryFilter { get => _selectedCategoryFilter; set => SetProperty(ref _selectedCategoryFilter, value); }
    public Supplier? SelectedSupplierFilter { get => _selectedSupplierFilter; set => SetProperty(ref _selectedSupplierFilter, value); }
    public string InventorySearchText { get => _inventorySearchText; set => SetProperty(ref _inventorySearchText, value); }
    public string InventoryActiveFilter { get => _inventoryActiveFilter; set => SetProperty(ref _inventoryActiveFilter, value); }
    public bool LowStockOnly { get => _lowStockOnly; set => SetProperty(ref _lowStockOnly, value); }
    public decimal InventoryQuantityInput { get => _inventoryQuantityInput; set => SetProperty(ref _inventoryQuantityInput, value); }
    public decimal InventoryCostPrice
    {
        get => InventoryProduct?.CostPrice ?? 0;
        set
        {
            if (InventoryProduct is null || InventoryProduct.CostPrice == value) return;
            InventoryProduct.CostPrice = value;
            Raise();
            RecalculateInventorySellingPrice();
        }
    }
    public decimal InventorySellingPercentage
    {
        get => _inventorySellingPercentage;
        set
        {
            if (!SetProperty(ref _inventorySellingPercentage, value) || _updatingInventoryPricing) return;
            RecalculateInventorySellingPrice();
        }
    }
    public decimal InventorySellingPrice
    {
        get => InventoryProduct?.SellingPrice ?? 0;
        set
        {
            if (InventoryProduct is null || InventoryProduct.SellingPrice == value) return;
            InventoryProduct.SellingPrice = value;
            Raise();
            if (!_updatingInventoryPricing) UpdateInventorySellingPercentageFromPrices();
        }
    }
    public string StockAdjustmentNotes { get => _stockAdjustmentNotes; set => SetProperty(ref _stockAdjustmentNotes, value); }
    public Category? SelectedCategory { get => _selectedCategory; set => SetProperty(ref _selectedCategory, value); }
    public Supplier? SelectedSupplier { get => _selectedSupplier; set => SetProperty(ref _selectedSupplier, value); }
    public DateTime? ReportFrom { get => _reportFrom; set => SetProperty(ref _reportFrom, value); }
    public DateTime? ReportTo { get => _reportTo; set => SetProperty(ref _reportTo, value); }

    private Task ActivateLicenseAsync()
    {
        var dialog = new LicenseActivationWindow(_licenseService) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
        _licenseStatus = _licenseService.GetStatus();
        Raise(nameof(LicenseStatus));
        Raise(nameof(LicenseCustomer));
        Raise(nameof(LicenseEdition));
        Raise(nameof(LicenseExpiry));
        Raise(nameof(LicenseMessage));
        StatusMessage = _licenseStatus.Message;
        return Task.CompletedTask;
    }

    private async Task NavigateAsync(string? section)
    {
        SelectedSection = section ?? "Dashboard";
        await RefreshAsync();

        if (SelectedSection == "New Invoice" && SelectedInvoice is null)
            await CreateInvoiceAsync();
        if (SelectedSection is "New Quotation" or "Quotations")
        {
            await Quotations.InitializeAsync();
            if (SelectedSection == "New Quotation" && Quotations.CurrentQuotation is null)
                await Quotations.StartNewAsync();
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            Company = await _lookup.GetCompanySettingsAsync();
            await Replace(Customers, await _lookup.GetCustomersAsync());
            await Replace(Products, await _lookup.GetProductsAsync());
            await Replace(Invoices, await _invoices.SearchAsync(null, null, null));
            await Replace(Receipts, await _receipts.GetReceiptsAsync());
            Snapshot = await _dashboard.GetSnapshotAsync();
            await RefreshInventoryAsync();
            if (SelectedSection == "Inventory Reports") await RefreshReportsAsync();
            StatusMessage = "Data refreshed.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task SearchCurrentSectionAsync()
    {
        try
        {
            var term = SearchText?.Trim() ?? "";
            switch (SelectedSection)
            {
                case "Customers":
                {
                    var matches = (await _lookup.GetCustomersAsync()).Where(x => CustomerMatches(x, term)).ToList();
                    await Replace(Customers, matches);
                    SelectedCustomer = Customers.FirstOrDefault();
                    StatusMessage = $"{Customers.Count} customer(s) found.";
                    break;
                }
                case "Products and Services":
                {
                    var matches = await _inventory.SearchProductsAsync(term, active: true);
                    await Replace(Products, matches);
                    SelectedProduct = Products.FirstOrDefault();
                    StatusMessage = $"{Products.Count} product(s) or service(s) found.";
                    break;
                }
                case "Store / Inventory":
                    InventorySearchText = term;
                    await SearchInventoryAsync();
                    break;
                case "Inventory Reports":
                    await SearchInventoryReportsAsync(term);
                    break;
                case "Receipts":
                {
                    var matches = (await _receipts.GetReceiptsAsync()).Where(x => ReceiptMatches(x, term)).ToList();
                    await Replace(Receipts, matches);
                    StatusMessage = $"{Receipts.Count} receipt(s) found.";
                    break;
                }
                case "New Quotation":
                case "Quotations":
                    await Quotations.ApplySearchAsync(term);
                    SelectedSection = "Quotations";
                    StatusMessage = Quotations.StatusMessage;
                    break;
                default:
                    await Replace(Invoices, await _invoices.SearchAsync(term, null, null));
                    SelectedSection = "Invoices";
                    StatusMessage = $"{Invoices.Count} invoice(s) found.";
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search failed: {ex.Message}";
        }
    }

    private async Task SearchInventoryAsync()
    {
        await RefreshInventoryAsync();
        StatusMessage = $"{InventoryProducts.Count} inventory item(s) found.";
    }

    private async Task SearchInventoryReportsAsync(string term)
    {
        await RefreshReportsAsync();
        if (!string.IsNullOrWhiteSpace(term))
        {
            await Replace(CurrentStockReport, CurrentStockReport.Where(x =>
                Contains(x.ReferenceNumber, term) || Contains(x.Product, term) || Contains(x.StorageLocation, term)).ToList());
            await Replace(LowStockReport, LowStockReport.Where(x =>
                Contains(x.ReferenceNumber, term) || Contains(x.Product, term) || Contains(x.StorageLocation, term)).ToList());
            await Replace(SalesProfitReport, SalesProfitReport.Where(x =>
                Contains(x.InvoiceReference, term) || Contains(x.Product, term)).ToList());
        }
        StatusMessage = $"{CurrentStockReport.Count} stock, {LowStockReport.Count} low-stock, and {SalesProfitReport.Count} sales row(s) found.";
    }

    private static bool CustomerMatches(Customer customer, string term)
        => string.IsNullOrWhiteSpace(term) || new[]
        {
            customer.CustomerCode, customer.Name, customer.CompanyName, customer.AttentionName,
            customer.ContactPerson, customer.Address, customer.DeliveryAddress, customer.Telephone,
            customer.Mobile, customer.Email, customer.TaxNumber, customer.CommercialRegistration, customer.Notes
        }.Any(value => Contains(value, term));

    private static bool ReceiptMatches(Receipt receipt, string term)
        => string.IsNullOrWhiteSpace(term) || new[]
        {
            receipt.ReferenceNumber, receipt.Invoice?.ReferenceNumber, receipt.Customer?.CustomerCode,
            receipt.Customer?.Name, receipt.PaymentMethod.ToString(), receipt.TransactionReference,
            receipt.ReceivedBy, receipt.Notes
        }.Any(value => Contains(value, term));

    private static bool Contains(string? value, string term)
        => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private async Task CreateInvoiceAsync()
    {
        try
        {
            SelectedCustomer ??= Customers.FirstOrDefault();
            if (SelectedCustomer is null) throw new InvalidOperationException("Create a customer first.");
            SelectedInvoice = await _invoices.CreateDraftAsync(SelectedCustomer.Id);
            if (SelectedProduct is null) SelectedProduct = Products.FirstOrDefault();
            SelectedSection = "New Invoice";
            StatusMessage = $"New invoice {SelectedInvoice.ReferenceNumber} is ready.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task AddInvoiceRowAsync()
    {
        if (SelectedInvoice is null)
        {
            await CreateInvoiceAsync();
            if (SelectedInvoice is null) return;
        }

        var product = SelectedProduct ?? Products.FirstOrDefault();
        var item = new InvoiceItem
        {
            SortOrder = InvoiceItems.Count + 1,
            ProductId = product?.Id,
            ProductReferenceSnapshot = product?.Code,
            Description = product?.Description ?? "Custom item",
            Unit = product?.Unit ?? "ea",
            Quantity = 1,
            UnitPrice = product?.SellingPrice ?? 0,
            CostPriceSnapshot = product?.CostPrice ?? 0,
            TaxPercentage = product?.TaxPercentage ?? Company.DefaultTaxPercentage
        };
        SelectedInvoice.Items.Add(item);
        InvoiceItems.Add(item);
        StatusMessage = "Invoice row added.";
    }

    private async Task SelectProductAsync()
    {
        var dialog = new ProductSelectionDialog(Products.Where(x => x.IsActive).ToList()) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.SelectedProduct is null) return;
        SelectedProduct = dialog.SelectedProduct;
        await AddInvoiceRowAsync();
    }

    private Task RemoveInvoiceRowAsync(InvoiceItem? item)
    {
        if (SelectedInvoice is null || item is null) return Task.CompletedTask;
        SelectedInvoice.Items.Remove(item);
        InvoiceItems.Remove(item);
        StatusMessage = "Invoice row removed.";
        return Task.CompletedTask;
    }

    private async Task SaveInvoiceAsync()
    {
        try
        {
            if (SelectedInvoice is null)
            {
                StatusMessage = "Create or select an invoice before saving.";
                return;
            }

            if (SelectedCustomer is null) throw new InvalidOperationException("Select a customer before saving the invoice.");
            SelectedInvoice.CustomerId = SelectedCustomer.Id;
            SelectedInvoice.Items = InvoiceItems.ToList();
            if (InvoiceItems.Any(x => x.ProductId is not null && x.UnitPrice < Products.FirstOrDefault(p => p.Id == x.ProductId)?.CostPrice))
            {
                if (MessageBox.Show("One or more invoice prices are below cost price. Save anyway?", "Below-cost warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            }
            SelectedInvoice.Status = InvoiceStatus.Finalized;
            await _invoices.SaveAsync(SelectedInvoice);
            await RefreshAsync();
            StatusMessage = $"Invoice {SelectedInvoice.ReferenceNumber} saved.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private Task EditInvoiceAsync(Invoice? invoice)
    {
        if (invoice is null) return Task.CompletedTask;
        SelectedInvoice = invoice;
        SelectedCustomer = Customers.FirstOrDefault(x => x.Id == invoice.CustomerId);
        SelectedSection = "New Invoice";
        StatusMessage = $"Modifying invoice {invoice.ReferenceNumber}. Save the invoice to apply your changes.";
        return Task.CompletedTask;
    }

    private async Task DeleteInvoiceAsync(Invoice? invoice)
    {
        if (invoice is null) return;
        var confirmation = new InvoiceDeleteConfirmationDialog(invoice.ReferenceNumber)
        {
            Owner = Application.Current.MainWindow
        };
        if (confirmation.ShowDialog() != true)
        {
            StatusMessage = $"Deletion of {invoice.ReferenceNumber} cancelled.";
            return;
        }

        try
        {
            await _invoices.SoftDeleteAsync(invoice.Id);
            await RefreshAsync();
            StatusMessage = $"Invoice {invoice.ReferenceNumber} deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not delete {invoice.ReferenceNumber}: {ex.Message}";
            MessageBox.Show(ex.Message, "Invoice deletion failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task MarkPaidAsync(Invoice? invoice)
    {
        if (invoice is null) return;
        await _invoices.MarkPaidAsync(invoice.Id);
        await RefreshAsync();
        StatusMessage = $"{invoice.ReferenceNumber} marked as paid.";
    }

    private async Task ExportInvoicePdfAsync(Invoice? invoice)
    {
        if (invoice is null) return;
        var dialog = new SaveFileDialog { Filter = "PDF document (*.pdf)|*.pdf", FileName = $"{invoice.ReferenceNumber}.pdf" };
        if (dialog.ShowDialog() != true) return;
        _pdf.ExportInvoice(invoice, Company, dialog.FileName);
        StatusMessage = $"PDF exported to {dialog.FileName}.";
        await Task.CompletedTask;
    }

    private async Task PrintInvoiceAsync(Invoice? invoice)
    {
        if (invoice is null) return;
        var file = Path.Combine(Path.GetTempPath(), $"{invoice.ReferenceNumber}.pdf");
        _pdf.ExportInvoice(invoice, Company, file);
        OpenPdfForPrinting(file);
        StatusMessage = $"Invoice {invoice.ReferenceNumber} opened. Use the PDF viewer print button or press Ctrl+P.";
        await Task.CompletedTask;
    }

    private async Task CreateReceiptAsync(Invoice? invoice)
    {
        try
        {
            if (invoice is null) return;
            var recordsExistingPayment = invoice.RemainingBalance <= 0 && invoice.AmountPaid > 0 && invoice.Receipts.Count == 0;
            if (invoice.RemainingBalance <= 0 && !recordsExistingPayment)
            {
                StatusMessage = invoice.Receipts.Count > 0
                    ? $"Invoice {invoice.ReferenceNumber} already has a receipt."
                    : $"Invoice {invoice.ReferenceNumber} has no recorded payment to receipt.";
                return;
            }

            var dialog = new PaymentReceiptDialog(invoice, Company.CurrencySymbol, recordsExistingPayment)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true)
            {
                StatusMessage = "Receipt cancelled.";
                return;
            }

            var receipt = recordsExistingPayment
                ? await _receipts.CreateForRecordedPaymentAsync(invoice.Id, dialog.PaymentMethod, dialog.TransactionReference, dialog.Notes)
                : await _receipts.CreateForInvoiceAsync(invoice.Id, dialog.PaymentAmount, dialog.PaymentMethod, dialog.TransactionReference, dialog.Notes);
            Receipts.Add(receipt);
            SelectedSection = "Receipts";
            await RefreshAsync();
            StatusMessage = $"Receipt {receipt.ReferenceNumber} created.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Receipt creation failed", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private static bool CanCreateReceipt(Invoice? invoice)
        => invoice is not null && (invoice.RemainingBalance > 0 || (invoice.AmountPaid > 0 && invoice.Receipts.Count == 0));

    private async Task ExportReceiptForInvoiceAsync(Invoice? invoice)
    {
        try
        {
            if (invoice is null) return;
            var receipt = await _receipts.GetOrCreateForInvoicePdfAsync(invoice.Id);
            var dialog = new SaveFileDialog { Filter = "PDF document (*.pdf)|*.pdf", FileName = $"{receipt.ReferenceNumber}.pdf" };
            if (dialog.ShowDialog() != true) return;
            _pdf.ExportReceipt(receipt, Company, dialog.FileName);
            await RefreshAsync();
            SelectedSection = "Receipts";
            StatusMessage = $"Receipt PDF exported to {dialog.FileName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task ExportReceiptPdfAsync(Receipt? receipt)
    {
        if (receipt is null) return;
        var dialog = new SaveFileDialog { Filter = "PDF document (*.pdf)|*.pdf", FileName = $"{receipt.ReferenceNumber}.pdf" };
        if (dialog.ShowDialog() != true) return;
        _pdf.ExportReceipt(receipt, Company, dialog.FileName);
        StatusMessage = $"Receipt PDF exported to {dialog.FileName}.";
        await Task.CompletedTask;
    }

    private async Task PrintReceiptAsync(Receipt? receipt)
    {
        if (receipt is null) return;
        var file = Path.Combine(Path.GetTempPath(), $"{receipt.ReferenceNumber}.pdf");
        _pdf.ExportReceipt(receipt, Company, file);
        OpenPdfForPrinting(file);
        StatusMessage = $"Receipt {receipt.ReferenceNumber} opened. Use the PDF viewer print button or press Ctrl+P.";
        await Task.CompletedTask;
    }

    private async Task EditReceiptAsync(Receipt? receipt)
    {
        if (receipt is null) return;
        var dialog = new ReceiptEditDialog(receipt, Company.CurrencySymbol)
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true)
        {
            StatusMessage = $"Modification of {receipt.ReferenceNumber} cancelled.";
            return;
        }

        await _receipts.UpdateAsync(receipt.Id, dialog.ReceiptDate, dialog.PaymentMethod,
            dialog.TransactionReference, dialog.Notes, dialog.ReceivedBy);
        await RefreshAsync();
        StatusMessage = $"Receipt {receipt.ReferenceNumber} modified.";
    }

    private async Task DeleteReceiptAsync(Receipt? receipt)
    {
        if (receipt is null) return;
        if (MessageBox.Show(
                $"Delete receipt {receipt.ReferenceNumber}?\n\nIf this receipt applied a payment, the linked invoice balance will be recalculated.",
                "Delete receipt", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            StatusMessage = $"Deletion of {receipt.ReferenceNumber} cancelled.";
            return;
        }

        await _receipts.SoftDeleteAsync(receipt.Id);
        await RefreshAsync();
        StatusMessage = $"Receipt {receipt.ReferenceNumber} deleted.";
    }

    private async Task AddCustomerAsync()
    {
        try
        {
            var next = (await _lookup.GetCustomersAsync()).Count + 1;
            var customer = await _lookup.AddCustomerAsync(new Customer { Name = $"New Customer {next}", AttentionName = $"Attention Name {next}", Email = $"customer{DateTime.Now:yyyyMMddHHmmss}@example.com" });
            Customers.Add(customer);
            SelectedCustomer = customer;
            await Quotations.RefreshLookupsAsync(customer.Id);
            StatusMessage = $"Customer {customer.CustomerCode} added.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Add customer failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveCustomerAsync()
    {
        try
        {
            if (SelectedCustomer is null)
            {
                StatusMessage = "Select a customer before saving.";
                return;
            }

            await _lookup.SaveCustomerAsync(SelectedCustomer);
            await Quotations.RefreshLookupsAsync(SelectedCustomer.Id);
            StatusMessage = $"Customer {SelectedCustomer.CustomerCode} saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task DeleteCustomerAsync()
    {
        try
        {
            if (SelectedCustomer is null)
            {
                StatusMessage = "Select a customer before deleting.";
                return;
            }

            var confirm = MessageBox.Show($"Delete customer '{SelectedCustomer.Name}'?\nExisting invoices will stay unchanged.", "Delete customer", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                StatusMessage = "Delete customer cancelled.";
                return;
            }

            var deleted = SelectedCustomer;
            await _lookup.DeleteCustomerAsync(deleted.Id);
            Customers.Remove(deleted);
            SelectedCustomer = Customers.FirstOrDefault();
            await Quotations.RefreshLookupsAsync(SelectedCustomer?.Id);
            StatusMessage = $"Customer {deleted.CustomerCode} deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Delete customer failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task AddProductAsync()
    {
        var allProducts = await _inventory.SearchProductsAsync();
        var next = allProducts.Count + 1;
        while (allProducts.Any(x => x.Code.Equals($"ITEM-{next:0000}", StringComparison.OrdinalIgnoreCase)))
            next++;
        var product = await _lookup.AddProductAsync(new Product { Code = $"ITEM-{next:0000}", Description = $"New product or service {next}", SellingPrice = 100, Unit = "ea", TaxPercentage = Company.DefaultTaxPercentage });
        Products.Add(product);
        SelectedProduct = product;
        StatusMessage = "Product/service added.";
    }

    private async Task SaveCompanyAsync()
    {
        await _lookup.SaveCompanySettingsAsync(Company);
        StatusMessage = "Company settings saved.";
    }

    private Task ChooseLogoAsync()
    {
        var dialog = new OpenFileDialog { Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" };
        if (dialog.ShowDialog() == true)
        {
            var assets = Path.Combine(DatabasePaths.AppDataFolder, "Assets");
            Directory.CreateDirectory(assets);
            var target = Path.Combine(assets, Path.GetFileName(dialog.FileName));
            File.Copy(dialog.FileName, target, overwrite: true);
            Company.LogoPath = target;
            Raise(nameof(Company));
            StatusMessage = "Logo selected. Save company settings to keep it.";
        }
        return Task.CompletedTask;
    }

    private async Task BackupAsync()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Backup archive (*.zip)|*.zip",
                FileName = $"InvoiceSoftwareBackup-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
                Title = "Save backup"
            };
            if (dialog.ShowDialog() != true)
            {
                StatusMessage = "Backup cancelled.";
                return;
            }

            if (File.Exists(dialog.FileName))
            {
                var replace = MessageBox.Show("This backup file already exists. Replace it?", "Replace backup", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (replace != MessageBoxResult.Yes)
                {
                    StatusMessage = "Backup cancelled.";
                    return;
                }

                File.Delete(dialog.FileName);
            }

            var path = await _backup.BackupAsync(dialog.FileName);
            StatusMessage = $"Backup created: {path}";
            MessageBox.Show($"Backup created successfully:\n{path}", "Backup complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup failed: {ex.Message}";
            MessageBox.Show(ex.Message, "Backup failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RestoreAsync()
    {
        try
        {
            var dialog = new OpenFileDialog { Filter = "Backup archive (*.zip)|*.zip", Title = "Choose backup to restore" };
            if (dialog.ShowDialog() != true)
            {
                StatusMessage = "Restore cancelled.";
                return;
            }

            if (MessageBox.Show("Restore this backup? A safety backup will be created first and current data will be replaced.", "Confirm restore", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                StatusMessage = "Restore cancelled.";
                return;
            }

            await _backup.RestoreAsync(dialog.FileName);
            StatusMessage = "Restore prepared. Close and reopen the application to apply it.";
            MessageBox.Show("Restore prepared successfully. Please close and reopen the application to apply the restored data.", "Restore ready", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restore failed: {ex.Message}";
            MessageBox.Show(ex.Message, "Restore failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Task NewInventoryProductAsync()
    {
        InventoryProduct = new Product
        {
            Unit = "Piece", TaxPercentage = Company.DefaultTaxPercentage, TrackStock = true,
            IsActive = true, Type = ProductType.Product
        };
        InventoryQuantityInput = 0;
        StatusMessage = "Enter the new product details. Required fields are marked with *.";
        return Task.CompletedTask;
    }

    private void RecalculateInventorySellingPrice()
    {
        if (InventoryProduct is null) return;

        _updatingInventoryPricing = true;
        try
        {
            var sellingPrice = decimal.Round(
                InventoryProduct.CostPrice * (1 + (InventorySellingPercentage / 100m)),
                2,
                MidpointRounding.AwayFromZero);
            if (InventoryProduct.SellingPrice == sellingPrice) return;
            InventoryProduct.SellingPrice = sellingPrice;
            Raise(nameof(InventorySellingPrice));
        }
        finally
        {
            _updatingInventoryPricing = false;
        }
    }

    private void UpdateInventorySellingPercentageFromPrices()
    {
        var percentage = InventoryProduct is null || InventoryProduct.CostPrice == 0
            ? 0
            : decimal.Round(
                ((InventoryProduct.SellingPrice - InventoryProduct.CostPrice) / InventoryProduct.CostPrice) * 100m,
                2,
                MidpointRounding.AwayFromZero);

        _updatingInventoryPricing = true;
        try
        {
            SetProperty(ref _inventorySellingPercentage, percentage, nameof(InventorySellingPercentage));
        }
        finally
        {
            _updatingInventoryPricing = false;
        }
    }

    private async Task RefreshInventoryAsync()
    {
        var active = InventoryActiveFilter switch { "Active" => true, "Inactive" => false, _ => (bool?)null };
        await Replace(InventoryProducts, await _inventory.SearchProductsAsync(InventorySearchText, SelectedCategoryFilter?.Id, SelectedSupplierFilter?.Id, active, LowStockOnly));
        await Replace(Categories, await _inventory.GetCategoriesAsync());
        await Replace(Suppliers, await _inventory.GetSuppliersAsync());
        if (InventoryProduct is not null && InventoryProduct.Id != 0)
            InventoryProduct = InventoryProducts.FirstOrDefault(x => x.Id == InventoryProduct.Id);
    }

    private async Task SaveInventoryProductAsync()
    {
        try
        {
            if (InventoryProduct is null) { await NewInventoryProductAsync(); return; }
            var quantityChanged = InventoryProduct.Id == 0 || InventoryQuantityInput != InventoryProduct.CurrentQuantity;
            if (InventoryProduct.Id != 0 && quantityChanged && string.IsNullOrWhiteSpace(StockAdjustmentNotes))
                throw new InvalidOperationException("Enter a reason for the stock adjustment.");
            var saved = await _inventory.SaveProductAsync(InventoryProduct, quantityChanged ? InventoryQuantityInput : null, StockAdjustmentNotes);
            await RefreshInventoryAsync();
            InventoryProduct = InventoryProducts.FirstOrDefault(x => x.Id == saved.Id);
            await Replace(Products, await _lookup.GetProductsAsync());
            StatusMessage = $"Product {saved.Code} saved.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; MessageBox.Show(ex.Message, "Product validation", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async Task DeleteInventoryProductAsync()
    {
        if (InventoryProduct is null || InventoryProduct.Id == 0) return;
        if (MessageBox.Show($"Delete product '{InventoryProduct.Code} - {InventoryProduct.Name}'?", "Confirm product deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { await _inventory.DeleteProductAsync(InventoryProduct.Id); StatusMessage = "Product deleted."; }
        catch (InvalidOperationException ex) { StatusMessage = ex.Message; MessageBox.Show(ex.Message, "Product retained", MessageBoxButton.OK, MessageBoxImage.Information); }
        await RefreshInventoryAsync();
        await Replace(Products, await _lookup.GetProductsAsync());
        InventoryProduct = null;
    }

    private async Task DuplicateInventoryProductAsync()
    {
        if (InventoryProduct is null || InventoryProduct.Id == 0) return;
        var copy = await _inventory.DuplicateProductAsync(InventoryProduct.Id);
        await RefreshInventoryAsync();
        InventoryProduct = InventoryProducts.FirstOrDefault(x => x.Id == copy.Id);
        StatusMessage = $"Product duplicated as {copy.Code}.";
    }

    private async Task ViewStockHistoryAsync()
    {
        await Replace(StockMovements, await _inventory.GetMovementsAsync(InventoryProduct?.Id));
        StatusMessage = InventoryProduct is null ? "Showing all stock movements." : $"Showing stock history for {InventoryProduct.Code}.";
    }

    private async Task NewCategoryAsync() { SelectedCategory = new Category { IsActive = true }; Categories.Add(SelectedCategory); await Task.CompletedTask; }
    private async Task SaveCategoryAsync()
    {
        if (SelectedCategory is null) return;
        await _inventory.SaveCategoryAsync(SelectedCategory); await RefreshInventoryAsync(); StatusMessage = "Category saved.";
    }
    private async Task NewSupplierAsync() { SelectedSupplier = new Supplier { IsActive = true }; Suppliers.Add(SelectedSupplier); await Task.CompletedTask; }
    private async Task SaveSupplierAsync()
    {
        if (SelectedSupplier is null) return;
        await _inventory.SaveSupplierAsync(SelectedSupplier); await RefreshInventoryAsync(); StatusMessage = "Supplier saved.";
    }

    private async Task ExportProductsCsvAsync()
    {
        var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"products-{DateTime.Today:yyyyMMdd}.csv" };
        if (dialog.ShowDialog() != true) return;
        var rows = new List<string> { "ReferenceNumber,Barcode,ProductName,Description,Category,Brand,Supplier,Unit,CostPrice,SellingPrice,TaxRate,Quantity,MinimumStock,ReorderQuantity,StorageLocation,ShelfBin,Status,TrackStock,Notes" };
        rows.AddRange(InventoryProducts.Select(x => string.Join(',', new[]
        {
            x.Code, x.Barcode, x.Name, x.Description, x.CategoryRecord?.Name ?? x.Category, x.Brand, x.Supplier?.CompanyName,
            x.Unit, x.CostPrice.ToString(System.Globalization.CultureInfo.InvariantCulture), x.SellingPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
            x.TaxPercentage.ToString(System.Globalization.CultureInfo.InvariantCulture), x.CurrentQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            x.MinimumQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture), x.ReorderQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            x.StorageLocation, x.ShelfBinNumber, x.IsActive ? "Active" : "Inactive", x.TrackStock ? "Yes" : "No", x.Notes
        }.Select(Csv))));
        await File.WriteAllLinesAsync(dialog.FileName, rows, Encoding.UTF8); StatusMessage = $"Products exported to {dialog.FileName}.";
    }

    private async Task ImportProductsCsvAsync()
    {
        var dialog = new OpenFileDialog { Filter = "CSV file (*.csv)|*.csv" };
        if (dialog.ShowDialog() != true) return;
        var lines = await File.ReadAllLinesAsync(dialog.FileName);
        if (lines.Length < 2) throw new InvalidOperationException("The CSV file contains no product rows.");
        var header = ParseCsv(lines[0]).Select((name, index) => (name, index)).ToDictionary(x => x.name.Trim(), x => x.index, StringComparer.OrdinalIgnoreCase);
        string Cell(string[] cells, string name) => header.TryGetValue(name, out var i) && i < cells.Length ? cells[i].Trim() : "";
        decimal Number(string value) => decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;
        var categories = (await _inventory.GetCategoriesAsync()).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var suppliers = (await _inventory.GetSuppliersAsync()).ToDictionary(x => x.CompanyName, StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        foreach (var line in lines.Skip(1).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var cells = ParseCsv(line);
            var code = Cell(cells, "ReferenceNumber");
            if (string.IsNullOrWhiteSpace(code)) continue;
            var existing = (await _inventory.SearchProductsAsync(code)).FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            var product = existing ?? new Product();
            product.Code = code; product.Barcode = Cell(cells, "Barcode"); product.Name = Cell(cells, "ProductName");
            product.Description = Cell(cells, "Description"); product.Brand = Cell(cells, "Brand"); product.Unit = Cell(cells, "Unit");
            var categoryName = Cell(cells, "Category");
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                if (!categories.TryGetValue(categoryName, out var category)) { category = await _inventory.SaveCategoryAsync(new Category { Name = categoryName, IsActive = true }); categories[categoryName] = category; }
                product.CategoryId = category.Id; product.Category = category.Name;
            }
            var supplierName = Cell(cells, "Supplier");
            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                if (!suppliers.TryGetValue(supplierName, out var supplier)) { supplier = await _inventory.SaveSupplierAsync(new Supplier { CompanyName = supplierName, IsActive = true }); suppliers[supplierName] = supplier; }
                product.SupplierId = supplier.Id;
            }
            product.CostPrice = Number(Cell(cells, "CostPrice")); product.SellingPrice = Number(Cell(cells, "SellingPrice"));
            product.TaxPercentage = Number(Cell(cells, "TaxRate")); product.MinimumQuantity = Number(Cell(cells, "MinimumStock"));
            product.ReorderQuantity = Number(Cell(cells, "ReorderQuantity")); product.StorageLocation = Cell(cells, "StorageLocation");
            product.ShelfBinNumber = Cell(cells, "ShelfBin"); product.IsActive = !Cell(cells, "Status").Equals("Inactive", StringComparison.OrdinalIgnoreCase);
            product.TrackStock = !Cell(cells, "TrackStock").Equals("No", StringComparison.OrdinalIgnoreCase); product.Notes = Cell(cells, "Notes");
            var quantity = Number(Cell(cells, "Quantity"));
            await _inventory.SaveProductAsync(product, quantity, "CSV import"); imported++;
        }
        await RefreshInventoryAsync(); await Replace(Products, await _lookup.GetProductsAsync()); StatusMessage = $"Imported {imported} product(s).";
    }

    private async Task PrintProductsAsync()
    {
        var document = new FlowDocument { PagePadding = new Thickness(40), FontFamily = new FontFamily("Segoe UI"), FontSize = 10 };
        document.Blocks.Add(new Paragraph(new Run($"{Company.CompanyName} - Product List")) { FontSize = 18, FontWeight = FontWeights.Bold });
        var table = new Table { CellSpacing = 0 }; foreach (var width in new[] { 90d, 160d, 90d, 70d, 70d, 70d }) table.Columns.Add(new TableColumn { Width = new GridLength(width) });
        var group = new TableRowGroup(); table.RowGroups.Add(group);
        void Row(params string[] values) { var row = new TableRow(); foreach (var value in values) row.Cells.Add(new TableCell(new Paragraph(new Run(value))) { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(.5), Padding = new Thickness(3) }); group.Rows.Add(row); }
        Row("Reference", "Product", "Category", "Sell Price", "Quantity", "Status");
        foreach (var x in InventoryProducts) Row(x.Code, x.Name, x.CategoryRecord?.Name ?? x.Category ?? "", x.SellingPrice.ToString("N2"), x.CurrentQuantity.ToString("N4"), x.IsActive ? "Active" : "Inactive");
        document.Blocks.Add(table); var dialog = new PrintDialog(); if (dialog.ShowDialog() == true) dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Product List"); await Task.CompletedTask;
    }

    private async Task RefreshReportsAsync()
    {
        await Replace(CurrentStockReport, await _inventory.GetCurrentStockReportAsync());
        await Replace(LowStockReport, await _inventory.GetCurrentStockReportAsync(true));
        await Replace(SalesProfitReport, await _inventory.GetSalesProfitReportAsync(ReportFrom, ReportTo));
        StatusMessage = "Inventory reports refreshed.";
    }
    private Task ExportStockReportAsync() => ExportReportAsync("current-stock", "Product,ReferenceNumber,Quantity,CostPrice,SellingPrice,TotalCostValue,PotentialSellingValue,StorageLocation", CurrentStockReport.Select(x => string.Join(',', new[] { x.Product, x.ReferenceNumber, x.Quantity.ToString(), x.CostPrice.ToString(), x.SellingPrice.ToString(), x.TotalCostValue.ToString(), x.PotentialSellingValue.ToString(), x.StorageLocation }.Select(Csv))));
    private Task ExportProfitReportAsync() => ExportReportAsync("sales-profit", "InvoiceReference,InvoiceDate,Product,Quantity,SellingPrice,CostPrice,Revenue,Cost,GrossProfit,GrossMarginPercentage", SalesProfitReport.Select(x => string.Join(',', new[] { x.InvoiceReference, x.InvoiceDate.ToString("yyyy-MM-dd"), x.Product, x.QuantitySold.ToString(), x.SellingPrice.ToString(), x.CostPrice.ToString(), x.Revenue.ToString(), x.Cost.ToString(), x.GrossProfit.ToString(), x.GrossMarginPercentage.ToString() }.Select(Csv))));
    private async Task ExportReportAsync(string name, string header, IEnumerable<string> rows)
    {
        var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"{name}-{DateTime.Today:yyyyMMdd}.csv" }; if (dialog.ShowDialog() != true) return;
        await File.WriteAllLinesAsync(dialog.FileName, new[] { header }.Concat(rows), Encoding.UTF8); StatusMessage = $"Report exported to {dialog.FileName}.";
    }
    private static string Csv(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
    private static string[] ParseCsv(string line)
    {
        var values = new List<string>(); var value = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++) { var c = line[i]; if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; } else if (c == '"') quoted = !quoted; else if (c == ',' && !quoted) { values.Add(value.ToString()); value.Clear(); } else value.Append(c); }
        values.Add(value.ToString()); return values.ToArray();
    }

    private static Task Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
        return Task.CompletedTask;
    }

    private static void OpenPdfForPrinting(string filePath)
    {
        Process.Start(new ProcessStartInfo(filePath)
        {
            UseShellExecute = true
        });
    }
}
