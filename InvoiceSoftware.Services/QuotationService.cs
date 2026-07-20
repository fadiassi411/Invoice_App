using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Services;

/// <summary>Owns the quotation lifecycle, snapshots, revisions, status audit, and invoice conversion transaction.</summary>
public sealed class QuotationService(
    InvoiceDbContext db,
    IReferenceNumberService references,
    QuotationCalculator quotationCalculator,
    InvoiceCalculator invoiceCalculator) : IQuotationService
{
    public async Task<Quotation> CreateDraftAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == customerId, cancellationToken)
            ?? throw new InvalidOperationException("Customer was not found.");
        var settings = await db.CompanySettings.AsNoTracking().FirstAsync(cancellationToken);
        var taxMode = Enum.TryParse<TaxMode>(settings.DefaultQuotationTaxMode, true, out var parsedTaxMode) ? parsedTaxMode : TaxMode.Exclusive;
        return new Quotation
        {
            QuotationNumber = await references.GenerateAsync(SequenceKind.Quotation, cancellationToken),
            CustomerId = customer.Id,
            QuotationDate = DateTime.Today,
            ValidUntil = DateTime.Today.AddDays(Math.Max(1, settings.DefaultQuotationValidityDays)),
            CurrencyCode = settings.Currency,
            TaxMode = taxMode,
            PaymentTerms = settings.DefaultQuotationPaymentTerms ?? settings.PaymentTerms,
            DeliveryTerms = settings.DefaultQuotationDeliveryTerms,
            Warranty = settings.DefaultQuotationWarranty,
            TermsAndConditions = settings.QuotationTermsAndConditions,
            Salesperson = settings.ContactPersonName,
            CustomerNameSnapshot = customer.Name,
            CustomerCompanySnapshot = customer.CompanyName ?? customer.Name,
            BillingAddressSnapshot = customer.Address,
            ProjectAddressSnapshot = customer.DeliveryAddress,
            ContactPersonSnapshot = customer.AttentionName ?? customer.ContactPerson,
            TelephoneSnapshot = customer.Telephone,
            MobileSnapshot = customer.Mobile,
            EmailSnapshot = customer.Email,
            TaxNumberSnapshot = customer.TaxNumber
        };
    }

    public async Task<Quotation> SaveAsync(Quotation quotation, CancellationToken cancellationToken = default)
    {
        await ValidateAndSnapshotAsync(quotation, cancellationToken);
        quotationCalculator.Calculate(quotation);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        QuotationStatus? previousStatus = null;
        if (quotation.Id == 0)
        {
            if (await db.Quotations.IgnoreQueryFilters().AnyAsync(x => x.QuotationNumber == quotation.QuotationNumber && x.RevisionNumber == quotation.RevisionNumber, cancellationToken))
                throw new InvalidOperationException("This quotation number and revision already exist.");
            db.Quotations.Add(quotation);
            quotation.StatusHistory.Add(History(null, quotation.Status, "Quotation created."));
            db.AuditLogs.Add(new AuditLog { Action = "Create", EntityName = nameof(Quotation), Details = quotation.DisplayNumber });
        }
        else
        {
            var persisted = await db.Quotations.AsNoTracking().SingleAsync(x => x.Id == quotation.Id, cancellationToken);
            previousStatus = persisted.Status;
            EnsureEditable(persisted.Status);
            var retainedIds = quotation.Items.Where(x => x.Id != 0).Select(x => x.Id).ToList();
            await db.QuotationItems.Where(x => x.QuotationId == quotation.Id && !retainedIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
            db.Quotations.Update(quotation);
            if (previousStatus != quotation.Status)
                quotation.StatusHistory.Add(History(previousStatus, quotation.Status, "Status changed while saving."));
            db.AuditLogs.Add(new AuditLog { Action = "Modify", EntityName = nameof(Quotation), EntityId = quotation.Id, Details = quotation.DisplayNumber });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return quotation;
    }

    public Task<Quotation?> GetAsync(int quotationId, CancellationToken cancellationToken = default)
        => db.Quotations.Include(x => x.Items)
            .Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.Id == quotationId, cancellationToken);

    public async Task<List<Quotation>> SearchAsync(QuotationSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        await ExpireDueQuotationsAsync(cancellationToken);
        var query = db.Quotations.AsNoTracking().Include(x => x.Items).Include(x => x.StatusHistory).AsQueryable();
        if (!criteria.IncludeArchived) query = query.Where(x => !x.IsArchived);
        if (!string.IsNullOrWhiteSpace(criteria.Text))
        {
            var text = criteria.Text.Trim();
            query = query.Where(x => x.QuotationNumber.Contains(text) || x.CustomerNameSnapshot.Contains(text) ||
                                     (x.Subject ?? "").Contains(text) || (x.ProjectName ?? "").Contains(text) ||
                                     (x.CustomerReference ?? "").Contains(text));
        }
        if (criteria.CustomerId is not null) query = query.Where(x => x.CustomerId == criteria.CustomerId);
        if (criteria.From is not null) query = query.Where(x => x.QuotationDate >= criteria.From.Value.Date);
        if (criteria.To is not null) query = query.Where(x => x.QuotationDate < criteria.To.Value.Date.AddDays(1));
        if (criteria.Status is not null) query = query.Where(x => x.Status == criteria.Status);
        if (criteria.Validity == QuotationValidityFilter.Valid) query = query.Where(x => x.ValidUntil >= DateTime.Today && x.Status != QuotationStatus.Expired);
        if (criteria.Validity == QuotationValidityFilter.Expired) query = query.Where(x => x.ValidUntil < DateTime.Today || x.Status == QuotationStatus.Expired);
        return await query.OrderByDescending(x => x.QuotationDate).ThenByDescending(x => x.Id).Take(500).ToListAsync(cancellationToken);
    }

    public async Task<Quotation> DuplicateAsync(int quotationId, CancellationToken cancellationToken = default)
    {
        var original = await LoadNoTrackingAsync(quotationId, cancellationToken);
        var copy = Clone(original, await references.GenerateAsync(SequenceKind.Quotation, cancellationToken), 0, null);
        copy.Status = QuotationStatus.Draft;
        return await SaveAsync(copy, cancellationToken);
    }

    public async Task<Quotation> CreateRevisionAsync(int quotationId, CancellationToken cancellationToken = default)
    {
        var original = await LoadNoTrackingAsync(quotationId, cancellationToken);
        var revision = await db.Quotations.IgnoreQueryFilters().Where(x => x.QuotationNumber == original.QuotationNumber)
            .MaxAsync(x => (int?)x.RevisionNumber, cancellationToken) ?? 0;
        var parentId = original.ParentQuotationId ?? original.Id;
        var copy = Clone(original, original.QuotationNumber, revision + 1, parentId);
        copy.Status = QuotationStatus.Draft;
        return await SaveAsync(copy, cancellationToken);
    }

    public async Task ChangeStatusAsync(int quotationId, QuotationStatus status, string? notes = null, CancellationToken cancellationToken = default)
    {
        var quotation = await db.Quotations.SingleOrDefaultAsync(x => x.Id == quotationId, cancellationToken)
            ?? throw new InvalidOperationException("Quotation was not found.");
        if (quotation.Status == status) return;
        if (!CanTransition(quotation.Status, status))
            throw new InvalidOperationException($"Quotation cannot change from {quotation.Status} to {status}.");
        var previous = quotation.Status;
        quotation.Status = status;
        db.QuotationStatusHistory.Add(History(previous, status, notes, quotation.Id));
        db.AuditLogs.Add(new AuditLog { Action = "StatusChange", EntityName = nameof(Quotation), EntityId = quotation.Id, Details = $"{previous} -> {status}. {notes}" });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(int quotationId, CancellationToken cancellationToken = default)
    {
        var quotation = await db.Quotations.SingleOrDefaultAsync(x => x.Id == quotationId, cancellationToken)
            ?? throw new InvalidOperationException("Quotation was not found.");
        quotation.IsArchived = true;
        db.AuditLogs.Add(new AuditLog { Action = "Archive", EntityName = nameof(Quotation), EntityId = quotation.Id, Details = quotation.DisplayNumber });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Invoice> ConvertToInvoiceAsync(int quotationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var quotation = await db.Quotations.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == quotationId, cancellationToken)
            ?? throw new InvalidOperationException("Quotation was not found.");
        if (quotation.ConvertedInvoiceId is not null || quotation.Status == QuotationStatus.ConvertedToInvoice)
            throw new InvalidOperationException("This quotation has already been converted to an invoice.");
        if (quotation.Status != QuotationStatus.Accepted)
            throw new InvalidOperationException("Mark the quotation as Accepted before converting it to an invoice.");

        quotationCalculator.Calculate(quotation);
        var reference = await references.GenerateAsync(SequenceKind.Invoice, cancellationToken);
        var invoice = new Invoice
        {
            ReferenceNumber = reference,
            InvoiceNumber = reference,
            SourceQuotationId = quotation.Id,
            SourceQuotationNumber = quotation.DisplayNumber,
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            CustomerId = quotation.CustomerId,
            CustomerNameSnapshot = quotation.CustomerNameSnapshot,
            CustomerAttentionNameSnapshot = quotation.ContactPersonSnapshot,
            CustomerAddressSnapshot = quotation.BillingAddressSnapshot,
            CustomerTelephoneSnapshot = quotation.TelephoneSnapshot,
            CustomerTaxNumberSnapshot = quotation.TaxNumberSnapshot,
            Currency = quotation.CurrencyCode,
            TaxMode = quotation.TaxMode,
            Status = InvoiceStatus.Draft,
            Notes = quotation.PublicNotes,
            ProjectName = quotation.ProjectName,
            ProjectLocation = quotation.ProjectLocation,
            CustomerReference = quotation.CustomerReference,
            PaymentTerms = quotation.PaymentTerms,
            DeliveryTerms = quotation.DeliveryTerms,
            Warranty = quotation.Warranty,
            OverallDiscount = quotation.OverallDiscountAmount,
            ShippingCharges = quotation.ShippingCharge,
            AdditionalCharges = quotation.AdditionalCharges,
            RoundingAdjustment = quotation.RoundingAdjustment,
            Items = quotation.Items.Where(x => x.ItemType != QuotationItemType.SectionHeader).OrderBy(x => x.DisplayOrder).Select((x, index) => new InvoiceItem
            {
                SortOrder = index + 1,
                ProductId = x.StockItemId,
                ProductReferenceSnapshot = x.ItemReferenceSnapshot,
                Description = x.DescriptionSnapshot,
                Quantity = x.Quantity,
                Unit = x.UnitSnapshot,
                UnitPrice = x.UnitPrice,
                DiscountPercentage = x.GrossAmount == 0 ? 0 : x.DiscountAmount / x.GrossAmount * 100m,
                TaxPercentage = x.TaxPercentage
            }).ToList()
        };
        var productIds = invoice.Items.Where(x => x.ProductId is not null).Select(x => x.ProductId!.Value).Distinct().ToList();
        var costs = await db.Products.IgnoreQueryFilters().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.CostPrice, cancellationToken);
        foreach (var item in invoice.Items.Where(x => x.ProductId is not null)) item.CostPriceSnapshot = costs.GetValueOrDefault(item.ProductId!.Value);
        invoiceCalculator.Calculate(invoice);
        if (invoice.GrandTotal != quotation.GrandTotal)
        {
            invoice.RoundingAdjustment += quotation.GrandTotal - invoice.GrandTotal;
            invoiceCalculator.Calculate(invoice);
        }

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        var previous = quotation.Status;
        quotation.ConvertedInvoiceId = invoice.Id;
        quotation.ConvertedInvoiceNumber = invoice.ReferenceNumber;
        quotation.Status = QuotationStatus.ConvertedToInvoice;
        db.QuotationStatusHistory.Add(History(previous, quotation.Status, $"Converted to invoice {invoice.ReferenceNumber}.", quotation.Id));
        db.AuditLogs.Add(new AuditLog { Action = "ConvertToInvoice", EntityName = nameof(Quotation), EntityId = quotation.Id, Details = $"{quotation.DisplayNumber} -> {invoice.ReferenceNumber}" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return invoice;
    }

    public async Task<QuotationStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await ExpireDueQuotationsAsync(cancellationToken);
        var rows = await db.Quotations.AsNoTracking().Where(x => !x.IsArchived).Select(x => new { x.Status, x.GrandTotal }).ToListAsync(cancellationToken);
        var accepted = rows.Count(x => x.Status is QuotationStatus.Accepted or QuotationStatus.ConvertedToInvoice);
        var converted = rows.Count(x => x.Status == QuotationStatus.ConvertedToInvoice);
        var decided = rows.Count(x => x.Status is QuotationStatus.Accepted or QuotationStatus.Rejected or QuotationStatus.ConvertedToInvoice);
        return new QuotationStatistics(rows.Count, rows.Count(x => x.Status == QuotationStatus.Draft), rows.Count(x => x.Status == QuotationStatus.Sent),
            accepted, rows.Count(x => x.Status == QuotationStatus.Rejected), rows.Count(x => x.Status == QuotationStatus.Expired), converted,
            rows.Sum(x => x.GrandTotal), decided == 0 ? 0 : Math.Round(accepted * 100m / decided, 2), accepted == 0 ? 0 : Math.Round(converted * 100m / accepted, 2));
    }

    private async Task ValidateAndSnapshotAsync(Quotation quotation, CancellationToken cancellationToken)
    {
        if (quotation.CustomerId <= 0 || !await db.Customers.AnyAsync(x => x.Id == quotation.CustomerId, cancellationToken))
            throw new InvalidOperationException("Select a customer.");
        if (quotation.ValidUntil.Date < quotation.QuotationDate.Date) throw new InvalidOperationException("Valid-until date cannot be earlier than the quotation date.");
        if (quotation.Items.Count == 0 || quotation.Items.All(x => x.ItemType == QuotationItemType.SectionHeader))
            throw new InvalidOperationException("Add at least one quotation item.");
        var settings = await db.CompanySettings.AsNoTracking().FirstAsync(cancellationToken);
        var ids = quotation.Items.Where(x => x.StockItemId is not null).Select(x => x.StockItemId!.Value).Distinct().ToList();
        var products = await db.Products.IgnoreQueryFilters().AsNoTracking().Include(x => x.CategoryRecord).Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var item in quotation.Items.OrderBy(x => x.DisplayOrder).Select((value, index) => (value, index)))
        {
            item.value.DisplayOrder = item.index + 1;
            item.value.LineNumber = item.index + 1;
            if (string.IsNullOrWhiteSpace(item.value.DescriptionSnapshot)) throw new InvalidOperationException($"Line {item.value.LineNumber}: description is required.");
            if (item.value.StockItemId is null) continue;
            if (!products.TryGetValue(item.value.StockItemId.Value, out var product)) throw new InvalidOperationException($"Line {item.value.LineNumber}: stock item was not found.");
            if (!settings.AllowQuotationPriceOverride && item.value.UnitPrice != product.SellingPrice) throw new InvalidOperationException($"Line {item.value.LineNumber}: price override is disabled.");
            if (!settings.AllowQuotationBelowCost && item.value.UnitPrice < product.CostPrice) throw new InvalidOperationException($"Line {item.value.LineNumber}: price is below cost.");
            if (settings.PreventQuotationAboveAvailableStock && product.TrackStock && item.value.Quantity > product.CurrentQuantity)
                throw new InvalidOperationException($"Line {item.value.LineNumber}: requested quantity exceeds available stock ({product.CurrentQuantity:0.####}).");
            item.value.ItemReferenceSnapshot ??= product.Code;
            item.value.PartNumberSnapshot ??= product.PartNumber;
            item.value.BarcodeSnapshot ??= product.Barcode;
            item.value.UnitSnapshot = string.IsNullOrWhiteSpace(item.value.UnitSnapshot) ? product.Unit : item.value.UnitSnapshot;
            item.value.BrandSnapshot ??= product.Brand;
            item.value.ManufacturerSnapshot ??= product.Manufacturer;
            item.value.WarrantySnapshot ??= product.Warranty;
            item.value.AvailableStockSnapshot = product.CurrentQuantity;
        }
    }

    private async Task<Quotation> LoadNoTrackingAsync(int id, CancellationToken cancellationToken)
        => await db.Quotations.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
           ?? throw new InvalidOperationException("Quotation was not found.");

    private static Quotation Clone(Quotation source, string number, int revision, int? parentId)
    {
        return new Quotation
        {
            QuotationNumber = number, RevisionNumber = revision, ParentQuotationId = parentId, CustomerId = source.CustomerId,
            QuotationDate = DateTime.Today, ValidUntil = source.ValidUntil < DateTime.Today ? DateTime.Today.AddDays(30) : source.ValidUntil,
            CurrencyCode = source.CurrencyCode, ExchangeRate = source.ExchangeRate, TaxMode = source.TaxMode,
            CustomerNameSnapshot = source.CustomerNameSnapshot, CustomerCompanySnapshot = source.CustomerCompanySnapshot,
            BillingAddressSnapshot = source.BillingAddressSnapshot, ProjectAddressSnapshot = source.ProjectAddressSnapshot,
            ContactPersonSnapshot = source.ContactPersonSnapshot, TelephoneSnapshot = source.TelephoneSnapshot, MobileSnapshot = source.MobileSnapshot,
            EmailSnapshot = source.EmailSnapshot, TaxNumberSnapshot = source.TaxNumberSnapshot, CustomerReference = source.CustomerReference,
            ProjectName = source.ProjectName, ProjectLocation = source.ProjectLocation, Subject = source.Subject, Salesperson = source.Salesperson,
            PaymentTerms = source.PaymentTerms, DeliveryTerms = source.DeliveryTerms, DeliveryPeriod = source.DeliveryPeriod, Warranty = source.Warranty,
            PublicNotes = source.PublicNotes, InternalNotes = source.InternalNotes, TermsAndConditions = source.TermsAndConditions,
            OverallDiscountType = source.OverallDiscountType, OverallDiscountValue = source.OverallDiscountValue,
            ShippingCharge = source.ShippingCharge, AdditionalCharges = source.AdditionalCharges, RoundingAdjustment = source.RoundingAdjustment,
            Items = source.Items.OrderBy(x => x.DisplayOrder).Select(x => new QuotationItem
            {
                LineNumber = x.LineNumber, DisplayOrder = x.DisplayOrder, ItemType = x.ItemType, StockItemId = x.StockItemId,
                ItemReferenceSnapshot = x.ItemReferenceSnapshot, PartNumberSnapshot = x.PartNumberSnapshot, BarcodeSnapshot = x.BarcodeSnapshot,
                DescriptionSnapshot = x.DescriptionSnapshot, UnitSnapshot = x.UnitSnapshot, BrandSnapshot = x.BrandSnapshot,
                ManufacturerSnapshot = x.ManufacturerSnapshot, WarrantySnapshot = x.WarrantySnapshot, AvailableStockSnapshot = x.AvailableStockSnapshot,
                Quantity = x.Quantity, UnitPrice = x.UnitPrice, DiscountType = x.DiscountType, DiscountPercentage = x.DiscountPercentage,
                DiscountValue = x.DiscountValue, TaxPercentage = x.TaxPercentage, Notes = x.Notes
            }).ToList()
        };
    }

    private async Task ExpireDueQuotationsAsync(CancellationToken cancellationToken)
    {
        var expired = await db.Quotations.Where(x => x.ValidUntil < DateTime.Today &&
            (x.Status == QuotationStatus.Draft || x.Status == QuotationStatus.Sent || x.Status == QuotationStatus.UnderReview)).ToListAsync(cancellationToken);
        foreach (var quotation in expired)
        {
            var previous = quotation.Status;
            quotation.Status = QuotationStatus.Expired;
            db.QuotationStatusHistory.Add(History(previous, quotation.Status, "Validity date elapsed.", quotation.Id));
        }
        if (expired.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureEditable(QuotationStatus status)
    {
        if (status is QuotationStatus.Accepted or QuotationStatus.ConvertedToInvoice or QuotationStatus.Cancelled or QuotationStatus.Expired)
            throw new InvalidOperationException($"A {status} quotation is locked. Create a revision to make changes.");
    }

    private static bool CanTransition(QuotationStatus from, QuotationStatus to) => from switch
    {
        QuotationStatus.Draft => to is QuotationStatus.Sent or QuotationStatus.Cancelled,
        QuotationStatus.Sent => to is QuotationStatus.UnderReview or QuotationStatus.Accepted or QuotationStatus.Rejected or QuotationStatus.Cancelled,
        QuotationStatus.UnderReview => to is QuotationStatus.Sent or QuotationStatus.Accepted or QuotationStatus.Rejected or QuotationStatus.Cancelled,
        QuotationStatus.Rejected => to is QuotationStatus.Draft or QuotationStatus.Cancelled,
        QuotationStatus.Accepted => to is QuotationStatus.Cancelled or QuotationStatus.ConvertedToInvoice,
        _ => false
    };

    private static QuotationStatusHistory History(QuotationStatus? previous, QuotationStatus next, string? notes, int quotationId = 0)
        => new() { QuotationId = quotationId, PreviousStatus = previous, NewStatus = next, Notes = notes };
}
