using System.Windows;
using System.Windows.Media;
using InvoiceSoftware.Licensing;
using Microsoft.Win32;

namespace InvoiceSoftware.App;

public partial class LicenseActivationWindow : Window
{
    private readonly IInvoiceLicenseService _licenseService;

    public LicenseActivationWindow(IInvoiceLicenseService licenseService)
    {
        InitializeComponent();
        _licenseService = licenseService;
        InstallationIdBox.Text = licenseService.InstallationId;
        DisplayStatus(licenseService.GetStatus());
    }

    private void CopyInstallationId_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_licenseService.InstallationId);
        StatusText.Text = "Installation ID copied to the clipboard.";
    }

    private void ActivateLicense_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Invoice App license",
            Filter = "Invoice App license (*.invoicelicense)|*.invoicelicense"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var status = _licenseService.InstallLicense(dialog.FileName);
            DisplayStatus(status);
            MessageBox.Show(this, "Invoice Maker was activated successfully.", "Activation complete", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            DisplayStatus(new InvoiceLicenseStatus(false, ex.Message));
            MessageBox.Show(this, ex.Message, "Activation failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void DisplayStatus(InvoiceLicenseStatus status)
    {
        StatusText.Text = status.Message;
        StatusPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(status.IsLicensed ? "#E6F6EF" : "#FFF4E5"));
        StatusPanel.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(status.IsLicensed ? "#77C9A5" : "#F3C77B"));
        StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(status.IsLicensed ? "#075E45" : "#7A4A00"));
    }
}
