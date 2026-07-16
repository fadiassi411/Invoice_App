namespace InvoiceSoftware.Core.Models;

public sealed class Invoice : BaseEntity
{
    public string ReferenceNumber { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public int? SourceQuotationId { get; set; }
    public Quotation? SourceQuotation { get; set; }
    public string? SourceQuotationNumber { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerNameSnapshot { get; set; }
    public string? CustomerAttentionNameSnapshot { get; set; }
    public string? CustomerAddressSnapshot { get; set; }
    public string? CustomerTelephoneSnapshot { get; set; }
    public string? CustomerTaxNumberSnapshot { get; set; }
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Cash;
    public string Currency { get; set; } = "USD";
    public TaxMode TaxMode { get; set; } = TaxMode.Exclusive;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public string? Notes { get; set; }
    public string? ProjectName { get; set; }
    public string? ProjectLocation { get; set; }
    public string? CustomerReference { get; set; }
    public string? PaymentTerms { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? Warranty { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal OverallDiscount { get; set; }
    public decimal TotalBeforeTax { get; set; }
    public decimal TotalTax { get; set; }
    public decimal ShippingCharges { get; set; }
    public decimal AdditionalCharges { get; set; }
    public decimal RoundingAdjustment { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal RemainingBalance { get; set; }
    public decimal GrandTotal { get; set; }
    public List<InvoiceItem> Items { get; set; } = [];
    public List<Receipt> Receipts { get; set; } = [];
}

public sealed class InvoiceItem : BaseEntity
{
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public int SortOrder { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public string? ProductReferenceSnapshot { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; } = 1m;
    public string Unit { get; set; } = "ea";
    public decimal UnitPrice { get; set; }
    public decimal CostPriceSnapshot { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal LineSubtotal { get; set; }
    public decimal LineDiscount { get; set; }
    public decimal LineTax { get; set; }
    public decimal LineTotal { get; set; }
}
