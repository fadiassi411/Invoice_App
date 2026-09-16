using System.ComponentModel.DataAnnotations.Schema;

namespace InvoiceSoftware.Core.Models;

public sealed class Quotation : BaseEntity
{
    public string QuotationNumber { get; set; } = "";
    public int RevisionNumber { get; set; }
    public int? ParentQuotationId { get; set; }
    public Quotation? ParentQuotation { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime QuotationDate { get; set; } = DateTime.Today;
    public DateTime ValidUntil { get; set; } = DateTime.Today.AddDays(30);
    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1m;
    public TaxMode TaxMode { get; set; } = TaxMode.Exclusive;

    public string CustomerNameSnapshot { get; set; } = "";
    public string? CustomerCompanySnapshot { get; set; }
    public string? BillingAddressSnapshot { get; set; }
    public string? ProjectAddressSnapshot { get; set; }
    public string? ContactPersonSnapshot { get; set; }
    public string? TelephoneSnapshot { get; set; }
    public string? MobileSnapshot { get; set; }
    public string? EmailSnapshot { get; set; }
    public string? TaxNumberSnapshot { get; set; }

    public string? CustomerReference { get; set; }
    public string? ProjectName { get; set; }
    public string? ProjectLocation { get; set; }
    public string? Subject { get; set; }
    public string? Salesperson { get; set; }
    public string? PaymentTerms { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? DeliveryPeriod { get; set; }
    public string? Warranty { get; set; }
    public string? PublicNotes { get; set; }
    public string? InternalNotes { get; set; }
    public string? TermsAndConditions { get; set; }

    public decimal Subtotal { get; set; }
    public decimal LineDiscountTotal { get; set; }
    public DiscountType OverallDiscountType { get; set; } = DiscountType.Percentage;
    public decimal OverallDiscountValue { get; set; }
    public decimal OverallDiscountAmount { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal ShippingCharge { get; set; }
    public decimal AdditionalCharges { get; set; }
    public decimal RoundingAdjustment { get; set; }
    public decimal GrandTotal { get; set; }
    public string? AmountInWords { get; set; }
    public bool HideItemPricesOnPdf { get; set; }

    public int? ConvertedInvoiceId { get; set; }
    public Invoice? ConvertedInvoice { get; set; }
    public string? ConvertedInvoiceNumber { get; set; }
    public bool IsArchived { get; set; }
    public List<QuotationItem> Items { get; set; } = [];
    public List<QuotationStatusHistory> StatusHistory { get; set; } = [];

    [NotMapped] public string DisplayNumber => $"{QuotationNumber} Rev. {RevisionNumber}";
    [NotMapped] public bool IsPastValidity => Status is not QuotationStatus.Accepted and not QuotationStatus.ConvertedToInvoice and not QuotationStatus.Cancelled && ValidUntil.Date < DateTime.Today;
}

public sealed class QuotationItem : BaseEntity
{
    public int QuotationId { get; set; }
    public Quotation? Quotation { get; set; }
    public int LineNumber { get; set; }
    public int DisplayOrder { get; set; }
    public QuotationItemType ItemType { get; set; } = QuotationItemType.StockItem;
    public int? StockItemId { get; set; }
    public Product? StockItem { get; set; }
    public string? ItemReferenceSnapshot { get; set; }
    public string? PartNumberSnapshot { get; set; }
    public string? BarcodeSnapshot { get; set; }
    public string DescriptionSnapshot { get; set; } = "";
    public string UnitSnapshot { get; set; } = "ea";
    public string? BrandSnapshot { get; set; }
    public string? ManufacturerSnapshot { get; set; }
    public string? WarrantySnapshot { get; set; }
    public decimal AvailableStockSnapshot { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
    public bool HideOnPdf { get; set; }
}

public sealed class QuotationStatusHistory : BaseEntity
{
    public int QuotationId { get; set; }
    public Quotation? Quotation { get; set; }
    public QuotationStatus? PreviousStatus { get; set; }
    public QuotationStatus NewStatus { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string ChangedBy { get; set; } = "System";
    public string? Notes { get; set; }
}
