using System.IO;
using System.Windows;
using InvoiceSoftware.App.ViewModels;
using InvoiceSoftware.Data;
using InvoiceSoftware.Licensing;
using InvoiceSoftware.Reporting;
using InvoiceSoftware.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InvoiceSoftware.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddDbContext<InvoiceDbContext>(options => options.UseSqlite(DatabasePaths.ConnectionString));
        services.AddInvoiceServices();
        services.AddSingleton<IInvoiceLicenseService, InvoiceLicenseService>();
        services.AddScoped<IInvoicePdfService, InvoicePdfService>();
        services.AddScoped<IQuotationPdfService, QuotationPdfService>();
        services.AddSingleton<QuotationWorkspaceViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

        var licenseService = _services.GetRequiredService<IInvoiceLicenseService>();
        if (!licenseService.GetStatus().IsLicensed)
        {
            var activation = new LicenseActivationWindow(licenseService);
            if (activation.ShowDialog() != true || !licenseService.GetStatus().IsLicensed)
            {
                Shutdown();
                return;
            }
        }

        ApplyPendingRestore();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
            await db.Database.MigrateAsync();
        }

        var window = _services.GetRequiredService<MainWindow>();
        window.DataContext = _services.GetRequiredService<MainViewModel>();
        MainWindow = window;
        window.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    private void OnExit(object sender, ExitEventArgs e) => _services?.Dispose();

    private static void ApplyPendingRestore()
    {
        if (!File.Exists(DatabasePaths.PendingRestoreDatabaseFile)) return;

        File.Copy(DatabasePaths.PendingRestoreDatabaseFile, DatabasePaths.DatabaseFile, overwrite: true);
        File.Delete(DatabasePaths.PendingRestoreDatabaseFile);
    }
}
