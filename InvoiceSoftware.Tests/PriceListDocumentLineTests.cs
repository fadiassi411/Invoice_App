using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;

namespace InvoiceSoftware.Tests;

public sealed class PriceListDocumentLineTests
{
    [Fact]
    public void Catalogue_item_becomes_independent_invoice_and_quotation_snapshots()
    {
        var source = new PriceListItem
        {
            ReferenceNumber = "00001234-A", ProductName = "Breaker", Description = "Breaker 20A",
            Brand = "Eaton", Unit = "Piece", SellingPrice = 19.335m, TaxRate = 6m, Currency = "USD"
        };

        var invoice = PriceListDocumentLineFactory.ForInvoice(source, "USD", 3);
        var quotation = PriceListDocumentLineFactory.ForQuotation(source, "USD", 4);
        source.Description = "Changed catalogue text";
        source.SellingPrice = 50m;

        Assert.Null(invoice.ProductId);
        Assert.Equal("00001234-A", invoice.ProductReferenceSnapshot);
        Assert.Equal("Breaker 20A", invoice.Description);
        Assert.Equal(19.335m, invoice.UnitPrice);
        Assert.Equal(6m, invoice.TaxPercentage);
        Assert.Null(quotation.StockItemId);
        Assert.Equal(QuotationItemType.ManualItem, quotation.ItemType);
        Assert.Equal("00001234-A", quotation.ItemReferenceSnapshot);
        Assert.Equal("Breaker 20A", quotation.DescriptionSnapshot);
        Assert.Equal(19.335m, quotation.UnitPrice);
        Assert.Null(quotation.CostPriceSnapshot);
    }

    [Fact]
    public void Currency_must_match_and_unspecified_currency_requires_confirmation()
    {
        var source = new PriceListItem { ReferenceNumber = "A", ProductName = "Item", Currency = "EUR" };
        Assert.Throws<InvalidOperationException>(() => PriceListDocumentLineFactory.ForInvoice(source, "USD", 1));
        Assert.Throws<InvalidOperationException>(() => PriceListDocumentLineFactory.ForQuotation(source, "USD", 1));

        source.Currency = null;
        Assert.Throws<InvalidOperationException>(() => PriceListDocumentLineFactory.ForInvoice(source, "USD", 1));
        Assert.NotNull(PriceListDocumentLineFactory.ForInvoice(source, "USD", 1, acceptUnspecifiedCurrency: true));

        source.IsActive = false;
        Assert.Throws<InvalidOperationException>(() => PriceListDocumentLineFactory.ForQuotation(source, "USD", 1, acceptUnspecifiedCurrency: true));
    }
}
