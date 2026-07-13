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

        foreach (var item in invoice.Items.OrderBy(i => i.SortOrder))
        {
            if (item.Quantity <= 0) throw new InvalidOperationException("Quantity must be greater than zero.");
            if (item.UnitPrice < 0) throw new InvalidOperationException("Unit price cannot be negative.");
            if (item.DiscountPercentage is < 0 or > 100) throw new InvalidOperationException("Discount must be between 0 and 100.");
            if (item.TaxPercentage is < 0 or > 100) throw new InvalidOperationException("Tax must be between 0 and 100.");

            item.LineSubtotal = Round(item.Quantity * item.UnitPrice);
            item.LineDiscount = Round(item.LineSubtotal * item.DiscountPercentage / 100m);
            var taxable = item.LineSubtotal - item.LineDiscount;
            item.LineTax = Round(taxable * item.TaxPercentage / 100m);
            item.LineTotal = Round(taxable + item.LineTax);
        }

        invoice.Subtotal = Round(invoice.Items.Sum(i => i.LineSubtotal));
        invoice.TotalDiscount = Round(invoice.Items.Sum(i => i.LineDiscount));
        invoice.TotalBeforeTax = Round(invoice.Subtotal - invoice.TotalDiscount);
        invoice.TotalTax = Round(invoice.Items.Sum(i => i.LineTax));
        invoice.GrandTotal = Round(invoice.TotalBeforeTax + invoice.TotalTax + invoice.ShippingCharges + invoice.PreviousBalance);
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

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record InvoiceTotals(decimal Subtotal, decimal Discount, decimal BeforeTax, decimal Tax, decimal GrandTotal, decimal Paid, decimal Remaining);
