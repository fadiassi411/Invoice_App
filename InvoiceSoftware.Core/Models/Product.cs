namespace InvoiceSoftware.Core.Models;

public sealed class Category : BaseEntity
{
    public string Name { get; set; } = "";
}

public sealed class Unit : BaseEntity
{
    public string Name { get; set; } = "";
    public string Abbreviation { get; set; } = "";
}

public sealed class Product : BaseEntity
{
    public string Code { get; set; } = "";
    public string? Barcode { get; set; }
    public string Description { get; set; } = "";
    public string? Category { get; set; }
    public ProductType Type { get; set; } = ProductType.Product;
    public string Unit { get; set; } = "ea";
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal DefaultDiscount { get; set; }
    public decimal CurrentQuantity { get; set; }
    public decimal MinimumQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
