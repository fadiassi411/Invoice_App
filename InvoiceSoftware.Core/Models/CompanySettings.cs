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
    public bool IncludeYearInReferences { get; set; } = true;
    public string InvoiceFooterNotes { get; set; } = "Thank you for your business.";
    public string PaymentTerms { get; set; } = "Total payment due in 30 days.";
    public string? BankInformation { get; set; }
    public string PreferredPaperSize { get; set; } = "A4";
    public string ApplicationDisplayName { get; set; } = "Invoice Management Software";
    public bool DeductInventoryOnInvoice { get; set; } = false;
    public bool AllowOverpayments { get; set; } = false;
}
