namespace InvoiceSoftware.Core.Models;

public sealed class Customer : BaseEntity
{
    public string CustomerCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? CompanyName { get; set; }
    public string? AttentionName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Address { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Telephone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? TaxNumber { get; set; }
    public string? CommercialRegistration { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal OpeningBalance { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Invoice> Invoices { get; set; } = [];
    public List<Quotation> Quotations { get; set; } = [];
    public List<Receipt> Receipts { get; set; } = [];
}
