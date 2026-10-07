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
    public string? Currency { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// The last mapping confirmed for a supplier's workbook layout. Header names,
/// rather than column positions, are stored so reordered files can be reused.
/// </summary>
public sealed class SupplierPriceListMapping : BaseEntity
{
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public string SheetName { get; set; } = "";
    public int HeaderRowNumber { get; set; }
    public string HeaderNamesJson { get; set; } = "[]";
    public string ColumnMappingsJson { get; set; } = "{}";
    public string ParsingSettingsJson { get; set; } = "{}";
}
