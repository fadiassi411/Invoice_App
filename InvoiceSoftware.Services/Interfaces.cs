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

public interface IQuotationService
{
    Task<Quotation> CreateDraftAsync(int customerId, CancellationToken cancellationToken = default);
    Task<Quotation> SaveAsync(Quotation quotation, CancellationToken cancellationToken = default);
    Task<Quotation?> GetAsync(int quotationId, CancellationToken cancellationToken = default);
    Task<List<Quotation>> SearchAsync(QuotationSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<Quotation> DuplicateAsync(int quotationId, CancellationToken cancellationToken = default);
    Task<Quotation> CreateRevisionAsync(int quotationId, CancellationToken cancellationToken = default);
    Task ChangeStatusAsync(int quotationId, QuotationStatus status, string? notes = null, CancellationToken cancellationToken = default);
    Task ArchiveAsync(int quotationId, CancellationToken cancellationToken = default);
    Task<Invoice> ConvertToInvoiceAsync(int quotationId, CancellationToken cancellationToken = default);
    Task<QuotationStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

public interface IReceiptService
{
    Task<Receipt> CreateForInvoiceAsync(int invoiceId, decimal amount, PaymentMethodType paymentMethod, string? transactionReference, string? notes = null, CancellationToken cancellationToken = default);
    Task<Receipt> CreateForRecordedPaymentAsync(int invoiceId, PaymentMethodType paymentMethod, string? transactionReference, string? notes = null, CancellationToken cancellationToken = default);
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

public interface IInventoryService
{
    Task<List<Product>> SearchProductsAsync(string? text = null, int? categoryId = null, int? supplierId = null, bool? active = null, bool lowStockOnly = false, CancellationToken cancellationToken = default);
    Task<Product> SaveProductAsync(Product product, decimal? adjustedQuantity = null, string? adjustmentNotes = null, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<Product> DuplicateProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<Category> SaveCategoryAsync(Category category, CancellationToken cancellationToken = default);
    Task<List<Supplier>> GetSuppliersAsync(CancellationToken cancellationToken = default);
    Task<Supplier> SaveSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task<List<StockMovement>> GetMovementsAsync(int? productId = null, CancellationToken cancellationToken = default);
    Task<List<CurrentStockReportRow>> GetCurrentStockReportAsync(bool lowStockOnly = false, CancellationToken cancellationToken = default);
    Task<List<SalesProfitReportRow>> GetSalesProfitReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
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
    int TotalActiveProducts,
    decimal TotalQuantityInStock,
    decimal TotalInventoryCostValue,
    int LowStockProducts,
    int OutOfStockProducts,
    decimal SalesToday,
    decimal GrossProfitToday,
    IReadOnlyList<DashboardSalesPoint> MonthlySalesTrend,
    IReadOnlyList<DashboardSalesPoint> YearlySalesTrend);

public sealed record DashboardSalesPoint(
    string Label,
    string Period,
    decimal Amount,
    double BarHeight);

public sealed record QuotationSearchCriteria(
    string? Text = null,
    int? CustomerId = null,
    DateTime? From = null,
    DateTime? To = null,
    QuotationStatus? Status = null,
    QuotationValidityFilter Validity = QuotationValidityFilter.All,
    bool IncludeArchived = false);

public sealed record QuotationStatistics(
    int Total,
    int Draft,
    int Sent,
    int Accepted,
    int Rejected,
    int Expired,
    int Converted,
    decimal TotalQuotedValue,
    decimal AcceptanceRate,
    decimal ConversionRate);
