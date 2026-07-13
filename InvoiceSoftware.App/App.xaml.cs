using System.IO;
using System.Windows;
using InvoiceSoftware.App.ViewModels;
using InvoiceSoftware.Data;
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
        ApplyPendingRestore();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddDbContext<InvoiceDbContext>(options => options.UseSqlite(DatabasePaths.ConnectionString));
        services.AddInvoiceServices();
        services.AddScoped<IInvoicePdfService, InvoicePdfService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
            await db.Database.MigrateAsync();
        }

        var window = _services.GetRequiredService<MainWindow>();
        window.DataContext = _services.GetRequiredService<MainViewModel>();
        window.Show();
    }

    private void OnExit(object sender, ExitEventArgs e) => _services?.Dispose();

    private static void ApplyPendingRestore()
    {
        if (!File.Exists(DatabasePaths.PendingRestoreDatabaseFile)) return;

        File.Copy(DatabasePaths.PendingRestoreDatabaseFile, DatabasePaths.DatabaseFile, overwrite: true);
        File.Delete(DatabasePaths.PendingRestoreDatabaseFile);
    }
}
