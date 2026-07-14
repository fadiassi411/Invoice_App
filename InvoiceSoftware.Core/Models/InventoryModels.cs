namespace InvoiceSoftware.Core.Models;

public sealed class StockMovement : BaseEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ReferenceNumber { get; set; } = "";
    public StockMovementType TransactionType { get; set; }
    public decimal QuantityBefore { get; set; }
    public decimal QuantityChanged { get; set; }
    public decimal QuantityAfter { get; set; }
    public decimal CostPriceAtTransaction { get; set; }
    public int? RelatedInvoiceId { get; set; }
    public Invoice? RelatedInvoice { get; set; }
    public string? RelatedInvoiceReferenceNumber { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string Operator { get; set; } = "System";
    public string? Notes { get; set; }
}

public sealed record CurrentStockReportRow(
    int ProductId, string Product, string ReferenceNumber, decimal Quantity,
    decimal CostPrice, decimal SellingPrice, decimal TotalCostValue,
    decimal PotentialSellingValue, string? StorageLocation);

public sealed record SalesProfitReportRow(
    string InvoiceReference, DateTime InvoiceDate, string Product, decimal QuantitySold,
    decimal SellingPrice, decimal CostPrice, decimal Revenue, decimal Cost,
    decimal GrossProfit, decimal GrossMarginPercentage);
