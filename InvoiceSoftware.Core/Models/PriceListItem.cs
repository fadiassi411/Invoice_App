namespace InvoiceSoftware.Core.Models;

/// <summary>
/// A customer-facing catalogue entry. Price-list entries are deliberately
/// independent from stock products so importing a catalogue cannot change
/// inventory, quantities, costs, invoices, or quotations.
/// </summary>
public sealed class PriceListItem : BaseEntity
{
    public string ReferenceNumber { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string Unit { get; set; } = "Piece";
    public decimal SellingPrice { get; set; }
    public decimal TaxRate { get; set; }
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
}
