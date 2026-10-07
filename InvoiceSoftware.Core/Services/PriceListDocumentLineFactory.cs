using InvoiceSoftware.Core.Models;

namespace InvoiceSoftware.Core.Services;

/// <summary>Copies catalogue values into document snapshots without linking to inventory.</summary>
public static class PriceListDocumentLineFactory
{
    public static InvoiceItem ForInvoice(PriceListItem source, string documentCurrency, int sortOrder, bool acceptUnspecifiedCurrency = false)
    {
        Validate(source, documentCurrency, acceptUnspecifiedCurrency);
        return new InvoiceItem
        {
            SortOrder = sortOrder,
            ProductId = null,
            ProductReferenceSnapshot = source.ReferenceNumber,
            Description = Description(source),
            Unit = source.Unit,
            Quantity = 1m,
            UnitPrice = source.SellingPrice,
            CostPriceSnapshot = 0m,
            TaxPercentage = source.TaxRate
        };
    }

    public static QuotationItem ForQuotation(PriceListItem source, string documentCurrency, int displayOrder, bool acceptUnspecifiedCurrency = false)
    {
        Validate(source, documentCurrency, acceptUnspecifiedCurrency);
        return new QuotationItem
        {
            LineNumber = displayOrder,
            DisplayOrder = displayOrder,
            ItemType = QuotationItemType.ManualItem,
            StockItemId = null,
            ItemReferenceSnapshot = source.ReferenceNumber,
            DescriptionSnapshot = Description(source),
            UnitSnapshot = source.Unit,
            BrandSnapshot = source.Brand,
            Quantity = 1m,
            UnitPrice = source.SellingPrice,
            CostPriceSnapshot = null,
            TaxPercentage = source.TaxRate
        };
    }

    private static void Validate(PriceListItem source, string documentCurrency, bool acceptUnspecifiedCurrency)
    {
        if (source.IsDeleted || !source.IsActive) throw new InvalidOperationException("This Price List item is no longer active. Refresh the picker.");
        if (string.IsNullOrWhiteSpace(documentCurrency)) throw new InvalidOperationException("Set the document currency before adding a Price List item.");
        if (string.IsNullOrWhiteSpace(source.Currency))
        {
            if (!acceptUnspecifiedCurrency)
                throw new InvalidOperationException("This Price List item has no currency. Confirm that its price uses the document currency before adding it.");
        }
        else if (!string.Equals(source.Currency.Trim(), documentCurrency.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Price List item {source.ReferenceNumber} is priced in {source.Currency}; this document uses {documentCurrency}. Change the document currency or choose another item. Prices are not converted automatically.");
        }
    }

    private static string Description(PriceListItem source)
        => string.IsNullOrWhiteSpace(source.Description) ? source.ProductName : source.Description;
}
