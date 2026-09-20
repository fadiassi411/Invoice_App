namespace InvoiceSoftware.Core.Models;

public sealed class Category : BaseEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Product> Products { get; set; } = [];
}

public sealed class Supplier : BaseEntity
{
    public string CompanyName { get; set; } = "";
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Product> Products { get; set; } = [];
}

public sealed class Unit : BaseEntity
{
    public string Name { get; set; } = "";
    public string Abbreviation { get; set; } = "";
}

public sealed class Product : BaseEntity
{
    public string Code { get; set; } = "";
    public string? PartNumber { get; set; }
    public string? Barcode { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Category { get; set; }
    public int? CategoryId { get; set; }
    public Category? CategoryRecord { get; set; }
    public string? Brand { get; set; }
    public string? Manufacturer { get; set; }
    public string? Warranty { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public ProductType Type { get; set; } = ProductType.Product;
    public string Unit { get; set; } = "ea";
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal DefaultDiscount { get; set; }
    public decimal CurrentQuantity { get; set; }
    public decimal MinimumQuantity { get; set; }
    public decimal ReorderQuantity { get; set; }
    public string? StorageLocation { get; set; }
    public string? ShelfBinNumber { get; set; }
    public bool TrackStock { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public List<StockMovement> StockMovements { get; set; } = [];
    public string StockStatus => !IsActive ? "Inactive" : !TrackStock ? "Not Tracked" : CurrentQuantity <= 0 ? "Out of Stock" : CurrentQuantity <= MinimumQuantity ? "Low Stock" : "Normal";
}
