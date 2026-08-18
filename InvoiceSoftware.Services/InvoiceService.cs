using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Services;

public sealed class InvoiceService(InvoiceDbContext db, IReferenceNumberService references, InvoiceCalculator calculator) : IInvoiceService
{
    public async Task<Invoice> CreateDraftAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var company = await db.CompanySettings.FirstAsync(cancellationToken);
        var customer = await db.Customers.FindAsync([customerId], cancellationToken) ?? throw new InvalidOperationException("Customer was not found.");
        var reference = await references.GenerateAsync(SequenceKind.Invoice, cancellationToken);
        return new Invoice
        {
            ReferenceNumber = reference,
            InvoiceNumber = reference,
            CustomerId = customer.Id,
            Customer = customer,
            CustomerNameSnapshot = customer.Name,
            CustomerAttentionNameSnapshot = customer.AttentionName,
            CustomerAddressSnapshot = customer.Address,
            CustomerTelephoneSnapshot = customer.Telephone,
            CustomerTaxNumberSnapshot = customer.TaxNumber,
            Currency = company.Currency,
            PreviousBalance = customer.OpeningBalance
        };
    }

    public async Task SaveAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice.DueDate < invoice.InvoiceDate) throw new InvalidOperationException("Due date cannot be earlier than invoice date.");
        if (invoice.Items.Count == 0) throw new InvalidOperationException("Add at least one invoice item.");
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == invoice.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException("Customer was not found.");
        invoice.CustomerNameSnapshot = customer.Name;
        invoice.CustomerAttentionNameSnapshot = customer.AttentionName;
        invoice.CustomerAddressSnapshot = customer.Address;
        invoice.CustomerTelephoneSnapshot = customer.Telephone;
        invoice.CustomerTaxNumberSnapshot = customer.TaxNumber;
        invoice.Customer = null;
        foreach (var receipt in invoice.Receipts)
        {
            receipt.CustomerId = customer.Id;
            receipt.Customer = null;
        }
        foreach (var item in invoice.Items)
        {
            if (item.Quantity <= 0) throw new InvalidOperationException("Invoice quantity must be greater than zero.");
            if (item.UnitPrice < 0) throw new InvalidOperationException("Selling price cannot be negative.");
        }
        calculator.Calculate(invoice);

        var oldStatus = InvoiceStatus.Draft;
        var oldQuantities = new Dictionary<int, decimal>();
        if (invoice.Id != 0)
        {
            oldStatus = await db.Invoices.AsNoTracking().Where(x => x.Id == invoice.Id).Select(x => x.Status).SingleAsync(cancellationToken);
            oldQuantities = await db.InvoiceItems.AsNoTracking().Where(x => x.InvoiceId == invoice.Id && x.ProductId != null)
                .GroupBy(x => x.ProductId!.Value).ToDictionaryAsync(x => x.Key, x => x.Sum(i => i.Quantity), cancellationToken);
        }
        else if (await db.Invoices.AnyAsync(x => x.ReferenceNumber == invoice.ReferenceNumber, cancellationToken))
        {
            throw new InvalidOperationException("Invoice reference already exists.");
        }

        var productIds = invoice.Items.Where(x => x.ProductId != null).Select(x => x.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.IgnoreQueryFilters().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var item in invoice.Items.Where(x => x.ProductId != null))
        {
            if (!products.TryGetValue(item.ProductId!.Value, out var product)) throw new InvalidOperationException("An invoice product was not found.");
            if (string.IsNullOrWhiteSpace(item.ProductReferenceSnapshot)) item.ProductReferenceSnapshot = product.Code;
            if (item.CostPriceSnapshot == 0 && item.Id == 0) item.CostPriceSnapshot = product.CostPrice;
            if (string.IsNullOrWhiteSpace(item.Description)) item.Description = product.Description;
            if (string.IsNullOrWhiteSpace(item.Unit)) item.Unit = product.Unit;
            // Catalog entities are updated independently; never attach a stale product graph through an invoice line.
            item.Product = null;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SynchronizeStockAsync(invoice, oldStatus, oldQuantities, products, cancellationToken);
        if (invoice.Id == 0)
        {
            db.Invoices.Add(invoice);
            db.AuditLogs.Add(new AuditLog { Action = "Create", EntityName = nameof(Invoice), Details = invoice.ReferenceNumber });
        }
        else
        {
            var retainedItemIds = invoice.Items.Where(x => x.Id != 0).Select(x => x.Id).ToList();
            await db.InvoiceItems.Where(x => x.InvoiceId == invoice.Id && !retainedItemIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
            await db.Receipts.Where(x => x.InvoiceId == invoice.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CustomerId, customer.Id), cancellationToken);
            db.Invoices.Update(invoice);
            db.AuditLogs.Add(new AuditLog { Action = "Modify", EntityName = nameof(Invoice), EntityId = invoice.Id, Details = invoice.ReferenceNumber });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<List<Invoice>> SearchAsync(string? text, InvoiceStatus? status, PaymentStatus? paymentStatus, CancellationToken cancellationToken = default)
    {
        var query = db.Invoices.Include(x => x.Customer).Include(x => x.Items).Include(x => x.Receipts).AsQueryable();
        if (!string.IsNullOrWhiteSpace(text))
        {
            query = query.Where(x => x.ReferenceNumber.Contains(text) || (x.CustomerNameSnapshot ?? "").Contains(text) || (x.Customer != null && x.Customer.Name.Contains(text)));
        }
        if (status is not null) query = query.Where(x => x.Status == status);
        if (paymentStatus is not null) query = query.Where(x => x.PaymentStatus == paymentStatus);
        return query.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.Id).Take(250).ToListAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var invoice = await db.Invoices.Include(x => x.Items).Include(x => x.Receipts).FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken) ?? throw new InvalidOperationException("Invoice was not found.");
        if (invoice.Status == InvoiceStatus.Finalized)
        {
            var productIds = invoice.Items.Where(x => x.ProductId != null).Select(x => x.ProductId!.Value).Distinct().ToList();
            var products = await db.Products.IgnoreQueryFilters().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            await SynchronizeStockAsync(invoice, InvoiceStatus.Finalized,
                invoice.Items.Where(x => x.ProductId != null).GroupBy(x => x.ProductId!.Value).ToDictionary(x => x.Key, x => x.Sum(i => i.Quantity)),
                products, cancellationToken, treatAsCancellation: true);
            invoice.Status = InvoiceStatus.Cancelled;
        }
        invoice.IsDeleted = true;
        invoice.DeletedAt = DateTime.UtcNow;
        foreach (var receipt in invoice.Receipts)
        {
            receipt.IsDeleted = true;
            receipt.DeletedAt = DateTime.UtcNow;
            db.AuditLogs.Add(new AuditLog
            {
                Action = "SoftDeleteWithInvoice",
                EntityName = nameof(Receipt),
                EntityId = receipt.Id,
                Details = $"{receipt.ReferenceNumber} with {invoice.ReferenceNumber}"
            });
        }
        db.AuditLogs.Add(new AuditLog { Action = "SoftDelete", EntityName = nameof(Invoice), EntityId = invoice.Id, Details = invoice.ReferenceNumber });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkPaidAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.Include(x => x.Items).FirstAsync(x => x.Id == invoiceId, cancellationToken);
        calculator.Calculate(invoice);
        invoice.AmountPaid = invoice.GrandTotal;
        calculator.Calculate(invoice);
        db.AuditLogs.Add(new AuditLog { Action = "MarkPaid", EntityName = nameof(Invoice), EntityId = invoice.Id, Details = invoice.ReferenceNumber });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SynchronizeStockAsync(Invoice invoice, InvoiceStatus oldStatus, IReadOnlyDictionary<int, decimal> oldQuantities,
        IReadOnlyDictionary<int, Product> products, CancellationToken cancellationToken, bool treatAsCancellation = false)
    {
        var settings = await db.CompanySettings.AsNoTracking().FirstAsync(cancellationToken);
        var newQuantities = treatAsCancellation || invoice.Status != InvoiceStatus.Finalized
            ? new Dictionary<int, decimal>()
            : invoice.Items.Where(x => x.ProductId != null).GroupBy(x => x.ProductId!.Value).ToDictionary(x => x.Key, x => x.Sum(i => i.Quantity));
        var effectiveOld = oldStatus == InvoiceStatus.Finalized ? oldQuantities : new Dictionary<int, decimal>();

        foreach (var productId in effectiveOld.Keys.Union(newQuantities.Keys))
        {
            if (!products.TryGetValue(productId, out var product) || !product.TrackStock || product.Type == ProductType.Service) continue;
            var oldSold = effectiveOld.GetValueOrDefault(productId);
            var newSold = newQuantities.GetValueOrDefault(productId);
            var soldDelta = newSold - oldSold;
            if (soldDelta == 0) continue;
            var before = product.CurrentQuantity;
            var after = before - soldDelta;
            if (!settings.AllowSellingWhenStockIsInsufficient && after < 0)
                throw new InvalidOperationException($"Insufficient stock. Available quantity: {before:N4}.");

            product.CurrentQuantity = after;
            var type = treatAsCancellation || (oldStatus == InvoiceStatus.Finalized && invoice.Status != InvoiceStatus.Finalized)
                ? StockMovementType.InvoiceCancellation
                : oldStatus != InvoiceStatus.Finalized ? StockMovementType.Sale : StockMovementType.InvoiceModification;
            db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id, ReferenceNumber = product.Code, TransactionType = type,
                QuantityBefore = before, QuantityChanged = -soldDelta, QuantityAfter = after,
                CostPriceAtTransaction = product.CostPrice, RelatedInvoice = invoice.Id == 0 ? invoice : null,
                RelatedInvoiceId = invoice.Id == 0 ? null : invoice.Id, RelatedInvoiceReferenceNumber = invoice.ReferenceNumber,
                Notes = type == StockMovementType.InvoiceCancellation ? "Invoice cancellation/restoration" : "Automatic invoice stock synchronization"
            });
        }
    }
}
