using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;

namespace InvoiceSoftware.Tests;

public sealed class InvoiceCalculatorTests
{
    [Fact]
    public void Calculates_discount_tax_and_remaining_balance()
    {
        var invoice = new Invoice
        {
            AmountPaid = 50m,
            ShippingCharges = 10m,
            Items =
            [
                new InvoiceItem { Description = "Service", Quantity = 2, UnitPrice = 100m, DiscountPercentage = 10m, TaxPercentage = 5m, SortOrder = 1 }
            ]
        };

        var totals = new InvoiceCalculator().Calculate(invoice);

        Assert.Equal(200m, totals.Subtotal);
        Assert.Equal(20m, totals.Discount);
        Assert.Equal(180m, totals.BeforeTax);
        Assert.Equal(9m, totals.Tax);
        Assert.Equal(199m, totals.GrandTotal);
        Assert.Equal(149m, totals.Remaining);
        Assert.Equal(PaymentStatus.PartiallyPaid, invoice.PaymentStatus);
    }

    [Fact]
    public void Rejects_invoice_without_items()
    {
        Assert.Throws<InvalidOperationException>(() => new InvoiceCalculator().Calculate(new Invoice()));
    }

    [Fact]
    public void Rejects_invalid_discount_ranges()
    {
        var invoice = new Invoice { Items = [new InvoiceItem { Description = "Bad", Quantity = 1, UnitPrice = 10, DiscountPercentage = 120 }] };
        Assert.Throws<InvalidOperationException>(() => new InvoiceCalculator().Calculate(invoice));
    }
}
