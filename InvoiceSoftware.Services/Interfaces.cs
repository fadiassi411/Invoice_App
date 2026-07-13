using InvoiceSoftware.Core.Models;

namespace InvoiceSoftware.Services;

public interface IReferenceNumberService
{
    Task<string> GenerateAsync(SequenceKind kind, CancellationToken cancellationToken = default);
}

public interface IInvoiceService
{
    Task<Invoice> CreateDraftAsync(int customerId, CancellationToken cancellationToken = default);
    Task SaveAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task<List<Invoice>> SearchAsync(string? text, InvoiceStatus? status, PaymentStatus? paymentStatus, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int invoiceId, CancellationToken cancellationToken = default);
    Task MarkPaidAsync(int invoiceId, CancellationToken cancellationToken = default);
}

public interface IReceiptService
{
    Task<Receipt> CreateForInvoiceAsync(int invoiceId, decimal amount, PaymentMethodType paymentMethod, string? transactionReference, string? notes = null, CancellationToken cancellationToken = default);
    Task<Receipt> GetOrCreateForInvoicePdfAsync(int invoiceId, CancellationToken cancellationToken = default);
    Task<List<Receipt>> GetReceiptsAsync(CancellationToken cancellationToken = default);
}

public interface ILookupService
{
    Task<CompanySettings> GetCompanySettingsAsync(CancellationToken cancellationToken = default);
    Task SaveCompanySettingsAsync(CompanySettings settings, CancellationToken cancellationToken = default);
    Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default);
    Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken = default);
    Task<Customer> AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default);
    Task SaveCustomerAsync(Customer customer, CancellationToken cancellationToken = default);
    Task DeleteCustomerAsync(int customerId, CancellationToken cancellationToken = default);
    Task<Product> AddProductAsync(Product product, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

public interface IBackupService
{
    Task<string> BackupAsync(string destinationFolder, CancellationToken cancellationToken = default);
    Task RestoreAsync(string backupFile, CancellationToken cancellationToken = default);
}

public sealed record DashboardSnapshot(
    int TotalInvoices,
    int InvoicesToday,
    decimal MonthlySales,
    int PaidInvoices,
    int PartiallyPaidInvoices,
    int UnpaidInvoices,
    int OverdueInvoices,
    decimal OutstandingAmount,
    decimal PaymentsReceived,
    IReadOnlyList<Invoice> RecentInvoices,
    IReadOnlyList<Receipt> RecentReceipts);
