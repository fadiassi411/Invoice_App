using InvoiceSoftware.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceSoftware.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddInvoiceServices(this IServiceCollection services)
    {
        services.AddScoped<InvoiceCalculator>();
        services.AddScoped<IReferenceNumberService, ReferenceNumberService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IBackupService, BackupService>();
        return services;
    }
}
