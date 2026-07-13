using System.Globalization;
using System.Windows;
using InvoiceSoftware.Core.Models;

namespace InvoiceSoftware.App;

public partial class PaymentReceiptDialog : Window
{
    private readonly decimal _remainingBalance;
    private readonly string _currencySymbol;

    public PaymentReceiptDialog(Invoice invoice, string currencySymbol)
    {
        InitializeComponent();
        _remainingBalance = invoice.RemainingBalance;
        _currencySymbol = string.IsNullOrWhiteSpace(currencySymbol) ? "$" : currencySymbol;

        InvoiceSummary = $"Invoice {invoice.ReferenceNumber} - {invoice.CustomerNameSnapshot}";
        BalanceSummary = $"Remaining balance: {_currencySymbol} {_remainingBalance:N2}";
        PaymentAmountText = _remainingBalance.ToString("N2", CultureInfo.CurrentCulture);
        Notes = $"Down payment for invoice {invoice.ReferenceNumber}";
        DataContext = this;
    }

    public string InvoiceSummary { get; }
    public string BalanceSummary { get; }
    public Array PaymentMethods { get; } = Enum.GetValues(typeof(PaymentMethodType));
    public string PaymentAmountText { get; set; } = "";
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Cash;
    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }
    public decimal PaymentAmount { get; private set; }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(PaymentAmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            && !decimal.TryParse(PaymentAmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            MessageBox.Show("Enter a valid payment amount.", "Invalid amount", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (amount <= 0)
        {
            MessageBox.Show("Payment amount must be greater than zero.", "Invalid amount", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (amount > _remainingBalance)
        {
            MessageBox.Show($"Payment amount cannot be greater than the remaining balance ({_currencySymbol} {_remainingBalance:N2}).", "Invalid amount", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PaymentAmount = amount;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
