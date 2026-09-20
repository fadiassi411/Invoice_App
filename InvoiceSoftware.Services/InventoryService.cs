using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Services;

public sealed class InventoryService(InvoiceDbContext db) : IInventoryService
{
    public Task<List<Product>> SearchProductsAsync(string? text = null, int? categoryId = null, int? supplierId = null, bool? active = null, bool lowStockOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.Products.Include(x => x.CategoryRecord).Include(x => x.Supplier).AsQueryable();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var term = text.Trim();
            query = query.Where(x => x.Code.Contains(term) || (x.PartNumber ?? "").Contains(term) ||
                                     (x.Barcode ?? "").Contains(term) || x.Name.Contains(term) || x.Description.Contains(term) ||
                                     (x.Category ?? "").Contains(term) || (x.CategoryRecord != null && x.CategoryRecord.Name.Contains(term)) ||
                                     (x.Brand ?? "").Contains(term) || (x.Manufacturer ?? "").Contains(term) ||
                                     (x.Supplier != null && x.Supplier.CompanyName.Contains(term)) ||
                                     (x.StorageLocation ?? "").Contains(term) || (x.ShelfBinNumber ?? "").Contains(term));
        }
        if (categoryId is not null) query = query.Where(x => x.CategoryId == categoryId);
        if (supplierId is not null) query = query.Where(x => x.SupplierId == supplierId);
        if (active is not null) query = query.Where(x => x.IsActive == active);
        if (lowStockOnly) query = query.Where(x => x.TrackStock && x.CurrentQuantity <= x.MinimumQuantity);
        return query.OrderBy(x => x.Code).ToListAsync(cancellationToken);
    }

    public async Task<Product> SaveProductAsync(Product product, decimal? adjustedQuantity = null, string? adjustmentNotes = null, CancellationToken cancellationToken = default)
    {
        Validate(product);
        if (await db.Products.IgnoreQueryFilters().AnyAsync(x => x.Code == product.Code && x.Id != product.Id, cancellationToken))
            throw new InvalidOperationException("Reference number already exists.");
        if (!string.IsNullOrWhiteSpace(product.Barcode) && await db.Products.IgnoreQueryFilters().AnyAsync(x => x.Barcode == product.Barcode && x.Id != product.Id, cancellationToken))
            throw new InvalidOperationException("Barcode already exists.");
        if (product.CategoryId is not null)
            product.Category = await db.Categories.Where(x => x.Id == product.CategoryId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (product.Id == 0)
        {
            var opening = adjustedQuantity ?? product.CurrentQuantity;
            product.CurrentQuantity = opening;
            db.Products.Add(product);
            await db.SaveChangesAsync(cancellationToken);
            if (product.TrackStock && opening != 0) AddMovement(product, StockMovementType.OpeningStock, 0, opening, "Opening stock");
        }
        else
        {
            var persistedQuantity = await db.Products.AsNoTracking().Where(x => x.Id == product.Id).Select(x => x.CurrentQuantity).SingleAsync(cancellationToken);
            product.CurrentQuantity = persistedQuantity;
            db.Products.Update(product);
            if (adjustedQuantity is not null && adjustedQuantity != persistedQuantity)
            {
                if (adjustedQuantity < 0) throw new InvalidOperationException("Quantity cannot be negative.");
                product.CurrentQuantity = adjustedQuantity.Value;
                var delta = adjustedQuantity.Value - persistedQuantity;
                AddMovement(product, delta > 0 ? StockMovementType.ManualStockAddition : StockMovementType.ManualStockDeduction, persistedQuantity, delta, adjustmentNotes ?? "Manual stock adjustment");
            }
        }
        db.AuditLogs.Add(new AuditLog { Action = product.Id == 0 ? "Create" : "Update", EntityName = nameof(Product), EntityId = product.Id, Details = product.Code });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return product;
    }

    public async Task DeleteProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken) ?? throw new InvalidOperationException("Product was not found.");
        if (await db.InvoiceItems.AnyAsync(x => x.ProductId == productId, cancellationToken))
        {
            product.IsActive = false;
            db.AuditLogs.Add(new AuditLog { Action = "DeactivateReferenced", EntityName = nameof(Product), EntityId = product.Id, Details = product.Code });
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("This product is already used in an invoice and cannot be permanently deleted. It has been marked inactive.");
        }
        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Product> DuplicateProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        var source = await db.Products.AsNoTracking().SingleAsync(x => x.Id == productId, cancellationToken);
        var baseCode = source.Code + "-COPY";
        var code = baseCode;
        var suffix = 2;
        while (await db.Products.IgnoreQueryFilters().AnyAsync(x => x.Code == code, cancellationToken)) code = $"{baseCode}-{suffix++}";
        var copy = new Product
        {
            Code = code, Name = source.Name + " (Copy)", Description = source.Description, Category = source.Category,
            CategoryId = source.CategoryId, Brand = source.Brand, SupplierId = source.SupplierId, Type = source.Type,
            Unit = source.Unit, CostPrice = source.CostPrice, SellingPrice = source.SellingPrice, TaxPercentage = source.TaxPercentage,
            DefaultDiscount = source.DefaultDiscount, MinimumQuantity = source.MinimumQuantity, ReorderQuantity = source.ReorderQuantity,
            StorageLocation = source.StorageLocation, ShelfBinNumber = source.ShelfBinNumber, TrackStock = source.TrackStock,
            Notes = source.Notes, IsActive = source.IsActive
        };
        return await SaveProductAsync(copy, 0, "Duplicated product", cancellationToken);
    }

    public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) => db.Categories.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    public async Task<Category> SaveCategoryAsync(Category category, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category.Name)) throw new InvalidOperationException("Category name is required.");
        if (await db.Categories.AnyAsync(x => x.Name == category.Name && x.Id != category.Id, cancellationToken)) throw new InvalidOperationException("Category name already exists.");
        if (category.Id == 0) db.Categories.Add(category); else db.Categories.Update(category);
        await db.SaveChangesAsync(cancellationToken); return category;
    }
    public Task<List<Supplier>> GetSuppliersAsync(CancellationToken cancellationToken = default) => db.Suppliers.OrderBy(x => x.CompanyName).ToListAsync(cancellationToken);
    public async Task<Supplier> SaveSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(supplier.CompanyName)) throw new InvalidOperationException("Supplier name is required.");
        if (!string.IsNullOrWhiteSpace(supplier.Email) && !supplier.Email.Contains('@')) throw new InvalidOperationException("Supplier email address is not valid.");
        if (supplier.Id == 0) db.Suppliers.Add(supplier); else db.Suppliers.Update(supplier);
        await db.SaveChangesAsync(cancellationToken); return supplier;
    }
    public Task<List<StockMovement>> GetMovementsAsync(int? productId = null, CancellationToken cancellationToken = default)
    {
        var query = db.StockMovements.Include(x => x.Product).AsNoTracking().AsQueryable();
        if (productId is not null) query = query.Where(x => x.ProductId == productId);
        return query.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id).Take(1000).ToListAsync(cancellationToken);
    }
    public Task<List<CurrentStockReportRow>> GetCurrentStockReportAsync(bool lowStockOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.Products.AsNoTracking().Where(x => x.IsActive);
        if (lowStockOnly) query = query.Where(x => x.TrackStock && x.CurrentQuantity <= x.MinimumQuantity);
        return query.OrderBy(x => x.Code).Select(x => new CurrentStockReportRow(x.Id, x.Name, x.Code, x.CurrentQuantity, x.CostPrice, x.SellingPrice, x.CurrentQuantity * x.CostPrice, x.CurrentQuantity * x.SellingPrice, x.StorageLocation)).ToListAsync(cancellationToken);
    }
    public Task<List<SalesProfitReportRow>> GetSalesProfitReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = db.InvoiceItems.AsNoTracking().Where(x => x.Invoice != null && x.Invoice.Status == InvoiceStatus.Finalized);
        if (from is not null) query = query.Where(x => x.Invoice!.InvoiceDate >= from.Value.Date);
        if (to is not null) query = query.Where(x => x.Invoice!.InvoiceDate < to.Value.Date.AddDays(1));
        return query.OrderByDescending(x => x.Invoice!.InvoiceDate).Select(x => new SalesProfitReportRow(
            x.Invoice!.ReferenceNumber, x.Invoice.InvoiceDate, x.Description, x.Quantity, x.UnitPrice, x.CostPriceSnapshot,
            x.Quantity * x.UnitPrice, x.Quantity * x.CostPriceSnapshot, x.Quantity * (x.UnitPrice - x.CostPriceSnapshot),
            x.Quantity * x.UnitPrice == 0 ? 0 : ((x.Quantity * (x.UnitPrice - x.CostPriceSnapshot)) / (x.Quantity * x.UnitPrice)) * 100)).ToListAsync(cancellationToken);
    }

    private void AddMovement(Product product, StockMovementType type, decimal before, decimal change, string notes) => db.StockMovements.Add(new StockMovement
    {
        Product = product, ProductId = product.Id, ReferenceNumber = product.Code, TransactionType = type,
        QuantityBefore = before, QuantityChanged = change, QuantityAfter = before + change,
        CostPriceAtTransaction = product.CostPrice, Notes = notes
    });

    private static void Validate(Product product)
    {
        product.Code = product.Code.Trim();
        product.Name = string.IsNullOrWhiteSpace(product.Name) ? product.Description.Trim() : product.Name.Trim();
        if (string.IsNullOrWhiteSpace(product.Code)) throw new InvalidOperationException("Reference number is required.");
        if (string.IsNullOrWhiteSpace(product.Name)) throw new InvalidOperationException("Product name is required.");
        if (string.IsNullOrWhiteSpace(product.Description)) throw new InvalidOperationException("Description is required.");
        if (product.CostPrice < 0) throw new InvalidOperationException("Cost price cannot be negative.");
        if (product.SellingPrice < 0) throw new InvalidOperationException("Selling price cannot be negative.");
        if (product.MinimumQuantity < 0 || product.ReorderQuantity < 0) throw new InvalidOperationException("Quantity cannot be negative.");
        if (product.TaxPercentage is < 0 or > 100) throw new InvalidOperationException("Tax rate must be between 0 and 100.");
        if (product.Type == ProductType.Service) product.TrackStock = false;
    }
}
