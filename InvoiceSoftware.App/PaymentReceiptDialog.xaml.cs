using System.Globalization;
using System.Windows;
using InvoiceSoftware.Core.Models;

namespace InvoiceSoftware.App;

public partial class PaymentReceiptDialog : Window
{
    private readonly decimal _maximumAmount;
    private readonly bool _recordExistingPayment;
    private readonly string _currencySymbol;

    public PaymentReceiptDialog(Invoice invoice, string currencySymbol, bool recordExistingPayment = false)
    {
        InitializeComponent();
        _recordExistingPayment = recordExistingPayment;
        _maximumAmount = recordExistingPayment ? invoice.AmountPaid : invoice.RemainingBalance;
        _currencySymbol = string.IsNullOrWhiteSpace(currencySymbol) ? "$" : currencySymbol;

        InvoiceSummary = $"Invoice {invoice.ReferenceNumber} - {invoice.CustomerNameSnapshot}";
        BalanceSummary = recordExistingPayment
            ? $"Payment already recorded: {_currencySymbol} {invoice.AmountPaid:N2}"
            : $"Remaining balance: {_currencySymbol} {invoice.RemainingBalance:N2}";
        PaymentAmountText = _maximumAmount.ToString("N2", CultureInfo.CurrentCulture);
        Notes = recordExistingPayment
            ? $"Receipt for payment already recorded on invoice {invoice.ReferenceNumber}"
            : $"Down payment for invoice {invoice.ReferenceNumber}";
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
    public bool IsPaymentAmountReadOnly => _recordExistingPayment;

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

        if (amount > _maximumAmount)
        {
            MessageBox.Show($"Payment amount cannot be greater than {_currencySymbol} {_maximumAmount:N2}.", "Invalid amount", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_recordExistingPayment && amount != _maximumAmount)
        {
            MessageBox.Show("The receipt amount must match the payment already recorded on the invoice.", "Invalid amount", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PaymentAmount = amount;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
