namespace InvoiceSoftware.Core.Models;

public sealed class CompanySettings : BaseEntity
{
    public string CompanyName { get; set; } = "MicroBrain Embedded System Solutions";
    public string? ContactPersonName { get; set; } = "Fadi Assi";
    public string? LogoPath { get; set; }
    public string? StampOrSignaturePath { get; set; }
    public string? Address { get; set; }
    public string? Telephone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? TaxIdentificationNumber { get; set; }
    public string? CommercialRegistrationNumber { get; set; }
    public string Currency { get; set; } = "USD";
    public string CurrencySymbol { get; set; } = "$";
    public decimal DefaultTaxPercentage { get; set; } = 6.25m;
    public string InvoicePrefix { get; set; } = "INV";
    public string ReceiptPrefix { get; set; } = "REC";
    public string QuotationPrefix { get; set; } = "QUO";
    public bool IncludeYearInReferences { get; set; } = true;
    public bool ResetQuotationSequenceYearly { get; set; } = true;
    public int QuotationStartingSequence { get; set; } = 1;
    public int DefaultQuotationValidityDays { get; set; } = 30;
    public string DefaultQuotationTaxMode { get; set; } = "Exclusive";
    public string? DefaultQuotationPaymentTerms { get; set; }
    public string? DefaultQuotationDeliveryTerms { get; set; }
    public string? DefaultQuotationWarranty { get; set; }
    public string QuotationFooter { get; set; } = "This quotation is subject to the terms and conditions stated herein and remains valid until the indicated validity date.";
    public string? QuotationTermsAndConditions { get; set; }
    public bool AllowQuotationPriceOverride { get; set; } = true;
    public bool AllowQuotationBelowCost { get; set; }
    public bool ShowAvailableStockOnQuotationScreen { get; set; } = true;
    public bool ShowAvailableStockOnPrintedQuotation { get; set; }
    public bool PreventQuotationAboveAvailableStock { get; set; }
    public bool OpenQuotationPdfAfterExport { get; set; } = true;
    public string? DefaultQuotationPdfFolder { get; set; }
    public string InvoiceFooterNotes { get; set; } = "Thank you for your business.";
    public string PaymentTerms { get; set; } = "Total payment due in 30 days.";
    public string? BankInformation { get; set; }
    public string PreferredPaperSize { get; set; } = "A4";
    public string ApplicationDisplayName { get; set; } = "Invoice Management Software";
    public bool DeductInventoryOnInvoice { get; set; } = false;
    public bool AllowSellingWhenStockIsInsufficient { get; set; } = false;
    public bool AllowOverpayments { get; set; } = false;
}
