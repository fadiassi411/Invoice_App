using System.Globalization;
using System.Windows;
using InvoiceSoftware.Core.Models;

namespace InvoiceSoftware.App;

public partial class ReceiptEditDialog : Window
{
    public ReceiptEditDialog(Receipt receipt, string currencySymbol)
    {
        InitializeComponent();
        var symbol = string.IsNullOrWhiteSpace(currencySymbol) ? "$" : currencySymbol;
        ReceiptSummary = receipt.ReferenceNumber;
        InvoiceSummary = receipt.Invoice is null
            ? $"Customer: {receipt.Customer?.Name ?? "Unknown"} • Original invoice is no longer active"
            : $"Invoice: {receipt.Invoice.ReferenceNumber} • Customer: {receipt.Customer?.Name ?? receipt.Invoice.CustomerNameSnapshot}";
        ReceiptDate = receipt.ReceiptDate.Date;
        PaymentAmountDisplay = $"{symbol} {receipt.PaymentAmount.ToString("N2", CultureInfo.CurrentCulture)}";
        PaymentMethod = receipt.PaymentMethod;
        TransactionReference = receipt.TransactionReference;
        Notes = receipt.Notes;
        ReceivedBy = receipt.ReceivedBy;
        DataContext = this;
    }

    public string ReceiptSummary { get; }
    public string InvoiceSummary { get; }
    public string PaymentAmountDisplay { get; }
    public Array PaymentMethods { get; } = Enum.GetValues(typeof(PaymentMethodType));
    public DateTime ReceiptDate { get; set; }
    public PaymentMethodType PaymentMethod { get; set; }
    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }
    public string? ReceivedBy { get; set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ReceiptDate == default)
        {
            MessageBox.Show("Select a valid receipt date.", "Invalid date", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
