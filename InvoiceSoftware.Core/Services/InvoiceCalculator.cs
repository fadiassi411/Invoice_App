using InvoiceSoftware.Core.Models;

namespace InvoiceSoftware.Core.Services;

public sealed class InvoiceCalculator
{
    public InvoiceTotals Calculate(Invoice invoice)
    {
        if (invoice.Items.Count == 0)
        {
            throw new InvalidOperationException("An invoice must contain at least one item.");
        }

        var baseItems = invoice.Items.Where(i => i.ServicePercentage is null).ToList();
        foreach (var item in baseItems)
            CalculateLine(item, invoice.TaxMode);
        var serviceBase = Round(baseItems.Sum(i => i.LineTotal));
        foreach (var item in invoice.Items.Where(i => i.ServicePercentage is not null))
        {
            if (item.ProductId is not null) throw new InvalidOperationException("A percentage service cannot be linked to a stock item.");
            if (item.ServicePercentage is < 0 or > 100) throw new InvalidOperationException("Service percentage must be between 0 and 100.");
            item.Quantity = 1;
            item.Unit = "%";
            item.UnitPrice = Round(serviceBase * item.ServicePercentage!.Value / 100m);
            CalculateLine(item, invoice.TaxMode);
        }

        invoice.Subtotal = Round(invoice.Items.Sum(i => i.LineSubtotal));
        invoice.TotalDiscount = Round(invoice.Items.Sum(i => i.LineDiscount) + invoice.OverallDiscount);
        invoice.TotalTax = Round(invoice.Items.Sum(i => i.LineTax));
        invoice.TotalBeforeTax = invoice.TaxMode == TaxMode.Inclusive
            ? Round(invoice.Subtotal - invoice.TotalDiscount - invoice.TotalTax)
            : Round(invoice.Subtotal - invoice.TotalDiscount);
        invoice.GrandTotal = Round(invoice.TotalBeforeTax + invoice.TotalTax + invoice.ShippingCharges + invoice.AdditionalCharges + invoice.RoundingAdjustment + invoice.PreviousBalance);
        invoice.RemainingBalance = Round(invoice.GrandTotal - invoice.AmountPaid);
        invoice.PaymentStatus = invoice.AmountPaid <= 0
            ? PaymentStatus.Unpaid
            : invoice.AmountPaid < invoice.GrandTotal
                ? PaymentStatus.PartiallyPaid
                : invoice.AmountPaid == invoice.GrandTotal
                    ? PaymentStatus.Paid
                    : PaymentStatus.Overpaid;

        return new InvoiceTotals(invoice.Subtotal, invoice.TotalDiscount, invoice.TotalBeforeTax, invoice.TotalTax, invoice.GrandTotal, invoice.AmountPaid, invoice.RemainingBalance);
    }

    private static void CalculateLine(InvoiceItem item, TaxMode taxMode)
    {
        if (item.Quantity <= 0) throw new InvalidOperationException("Quantity must be greater than zero.");
        if (item.UnitPrice < 0) throw new InvalidOperationException("Unit price cannot be negative.");
        if (item.DiscountPercentage is < 0 or > 100) throw new InvalidOperationException("Discount must be between 0 and 100.");
        if (item.TaxPercentage is < 0 or > 100) throw new InvalidOperationException("Tax must be between 0 and 100.");
        item.LineSubtotal = Round(item.Quantity * item.UnitPrice);
        item.LineDiscount = Round(item.LineSubtotal * item.DiscountPercentage / 100m);
        var taxable = item.LineSubtotal - item.LineDiscount;
        item.LineTax = taxMode switch
        {
            TaxMode.Exempt => 0,
            TaxMode.Inclusive when item.TaxPercentage > 0 => Round(taxable - taxable / (1m + item.TaxPercentage / 100m)),
            _ => Round(taxable * item.TaxPercentage / 100m)
        };
        item.LineTotal = taxMode == TaxMode.Inclusive ? Round(taxable) : Round(taxable + item.LineTax);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record InvoiceTotals(decimal Subtotal, decimal Discount, decimal BeforeTax, decimal Tax, decimal GrandTotal, decimal Paid, decimal Remaining);
