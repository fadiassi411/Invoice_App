using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using InvoiceSoftware.App;
using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
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
    private readonly IInvoicePdfService _pdf;

    private string _selectedSection = "Dashboard";
    private string _statusMessage = "Ready";
    private string? _searchText;
    private CompanySettings _company = new();
    private Customer? _selectedCustomer;
    private Product? _selectedProduct;
    private Invoice? _selectedInvoice;
    private DashboardSnapshot? _snapshot;

    public MainViewModel(ILookupService lookup, IInvoiceService invoices, IReceiptService receipts, IDashboardService dashboard, IBackupService backup, IInvoicePdfService pdf)
    {
        _lookup = lookup;
        _invoices = invoices;
        _receipts = receipts;
        _dashboard = dashboard;
        _backup = backup;
        _pdf = pdf;

        Sections = ["Dashboard", "New Invoice", "Invoices", "Customers", "Products and Services", "Receipts", "Reports", "Backup and Restore", "Company Settings", "Application Settings"];
        Customers = [];
        Products = [];
        InvoiceItems = [];
        Invoices = [];
        Receipts = [];

        NavigateCommand = new RelayCommand<string>(NavigateAsync);
        RefreshCommand = new RelayCommand(RefreshAsync);
        NewInvoiceCommand = new RelayCommand(CreateInvoiceAsync);
        AddInvoiceRowCommand = new RelayCommand(AddInvoiceRowAsync);
        RemoveInvoiceRowCommand = new RelayCommand<InvoiceItem>(RemoveInvoiceRowAsync);
        SaveInvoiceCommand = new RelayCommand(SaveInvoiceAsync);
        DeleteInvoiceCommand = new RelayCommand<Invoice>(DeleteInvoiceAsync);
        MarkPaidCommand = new RelayCommand<Invoice>(MarkPaidAsync);
        ExportInvoicePdfCommand = new RelayCommand<Invoice>(ExportInvoicePdfAsync);
        PrintInvoiceCommand = new RelayCommand<Invoice>(PrintInvoiceAsync);
        CreateReceiptCommand = new RelayCommand<Invoice>(CreateReceiptAsync);
        ExportReceiptForInvoiceCommand = new RelayCommand<Invoice>(ExportReceiptForInvoiceAsync);
        ExportReceiptPdfCommand = new RelayCommand<Receipt>(ExportReceiptPdfAsync);
        PrintReceiptCommand = new RelayCommand<Receipt>(PrintReceiptAsync);
        AddCustomerCommand = new RelayCommand(AddCustomerAsync);
        SaveCustomerCommand = new RelayCommand(SaveCustomerAsync);
        DeleteCustomerCommand = new RelayCommand(DeleteCustomerAsync);
        AddProductCommand = new RelayCommand(AddProductAsync);
        SaveCompanyCommand = new RelayCommand(SaveCompanyAsync);
        ChooseLogoCommand = new RelayCommand(ChooseLogoAsync);
        BackupCommand = new RelayCommand(BackupAsync);
        RestoreCommand = new RelayCommand(RestoreAsync);
        _ = RefreshAsync();
    }

    public string[] Sections { get; }
    public ObservableCollection<Customer> Customers { get; }
    public ObservableCollection<Product> Products { get; }
    public ObservableCollection<Invoice> Invoices { get; }
    public ObservableCollection<InvoiceItem> InvoiceItems { get; }
    public ObservableCollection<Receipt> Receipts { get; }

    public ICommand NavigateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand NewInvoiceCommand { get; }
    public ICommand AddInvoiceRowCommand { get; }
    public ICommand RemoveInvoiceRowCommand { get; }
    public ICommand SaveInvoiceCommand { get; }
    public ICommand DeleteInvoiceCommand { get; }
    public ICommand MarkPaidCommand { get; }
    public ICommand ExportInvoicePdfCommand { get; }
    public ICommand PrintInvoiceCommand { get; }
    public ICommand CreateReceiptCommand { get; }
    public ICommand ExportReceiptForInvoiceCommand { get; }
    public ICommand ExportReceiptPdfCommand { get; }
    public ICommand PrintReceiptCommand { get; }
    public ICommand AddCustomerCommand { get; }
    public ICommand SaveCustomerCommand { get; }
    public ICommand DeleteCustomerCommand { get; }
    public ICommand AddProductCommand { get; }
    public ICommand SaveCompanyCommand { get; }
    public ICommand ChooseLogoCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand RestoreCommand { get; }

    public string SelectedSection { get => _selectedSection; set => SetProperty(ref _selectedSection, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string? SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
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

    private async Task NavigateAsync(string? section)
    {
        SelectedSection = section ?? "Dashboard";
        await RefreshAsync();

        if (SelectedSection == "New Invoice" && SelectedInvoice is null)
            await CreateInvoiceAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            Company = await _lookup.GetCompanySettingsAsync();
            await Replace(Customers, await _lookup.GetCustomersAsync());
            await Replace(Products, await _lookup.GetProductsAsync());
            await Replace(Invoices, await _invoices.SearchAsync(SearchText, null, null));
            await Replace(Receipts, await _receipts.GetReceiptsAsync());
            Snapshot = await _dashboard.GetSnapshotAsync();
            StatusMessage = "Data refreshed.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

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
            Description = product?.Description ?? "Custom item",
            Unit = product?.Unit ?? "ea",
            Quantity = 1,
            UnitPrice = product?.SellingPrice ?? 0,
            TaxPercentage = product?.TaxPercentage ?? Company.DefaultTaxPercentage
        };
        SelectedInvoice.Items.Add(item);
        InvoiceItems.Add(item);
        StatusMessage = "Invoice row added.";
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

            SelectedInvoice.Items = InvoiceItems.ToList();
            await _invoices.SaveAsync(SelectedInvoice);
            await RefreshAsync();
            StatusMessage = $"Invoice {SelectedInvoice.ReferenceNumber} saved.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task DeleteInvoiceAsync(Invoice? invoice)
    {
        if (invoice is null) return;
        if (MessageBox.Show($"Delete invoice {invoice.ReferenceNumber}? It can be recovered from deleted records.", "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await _invoices.SoftDeleteAsync(invoice.Id);
        await RefreshAsync();
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
            if (invoice.RemainingBalance <= 0)
            {
                StatusMessage = $"Invoice {invoice.ReferenceNumber} is already paid. No receipt can be created for a zero balance.";
                return;
            }

            var dialog = new PaymentReceiptDialog(invoice, Company.CurrencySymbol)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true)
            {
                StatusMessage = "Receipt cancelled.";
                return;
            }

            var receipt = await _receipts.CreateForInvoiceAsync(invoice.Id, dialog.PaymentAmount, dialog.PaymentMethod, dialog.TransactionReference, dialog.Notes);
            Receipts.Add(receipt);
            SelectedSection = "Receipts";
            await RefreshAsync();
            StatusMessage = $"Receipt {receipt.ReferenceNumber} created.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Receipt PDF failed", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

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

    private async Task AddCustomerAsync()
    {
        try
        {
            var next = Customers.Count + 1;
            var customer = await _lookup.AddCustomerAsync(new Customer { Name = $"New Customer {next}", AttentionName = $"Attention Name {next}", Email = $"customer{DateTime.Now:yyyyMMddHHmmss}@example.com" });
            Customers.Add(customer);
            SelectedCustomer = customer;
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
        var next = Products.Count + 1;
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
