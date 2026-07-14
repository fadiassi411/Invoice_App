using System.Windows;

namespace InvoiceSoftware.App;

public partial class InvoiceDeleteConfirmationDialog : Window
{
    public InvoiceDeleteConfirmationDialog(string referenceNumber)
    {
        InitializeComponent();
        ReferenceRun.Text = referenceNumber;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Delete_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
