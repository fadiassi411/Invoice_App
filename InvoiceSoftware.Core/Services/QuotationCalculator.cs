using InvoiceSoftware.Core.Models;

namespace InvoiceSoftware.Core.Services;

/// <summary>Calculates every persisted and displayed quotation total from decimal inputs.</summary>
public sealed class QuotationCalculator
{
    public QuotationTotals Calculate(Quotation quotation, int decimalPlaces = 2)
    {
        ArgumentNullException.ThrowIfNull(quotation);
        if (decimalPlaces is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
        if (quotation.Items.Count == 0) throw new InvalidOperationException("Add at least one quotation item.");

        foreach (var item in quotation.Items.OrderBy(x => x.DisplayOrder))
        {
            if (item.ItemType == QuotationItemType.SectionHeader)
            {
                item.Quantity = 0;
                item.UnitPrice = 0;
                item.GrossAmount = item.DiscountAmount = item.NetAmount = item.TaxAmount = item.LineTotal = 0;
                continue;
            }

            if (item.Quantity <= 0) throw new InvalidOperationException($"Line {item.LineNumber}: quantity must be greater than zero.");
            if (item.UnitPrice < 0) throw new InvalidOperationException($"Line {item.LineNumber}: unit price cannot be negative.");
            if (item.TaxPercentage is < 0 or > 100) throw new InvalidOperationException($"Line {item.LineNumber}: tax must be between 0 and 100.");

            item.GrossAmount = Round(item.Quantity * item.UnitPrice, decimalPlaces);
            item.DiscountAmount = item.DiscountType == DiscountType.Percentage
                ? PercentageDiscount(item, decimalPlaces)
                : Round(Math.Min(item.GrossAmount, Math.Max(0, item.DiscountValue)), decimalPlaces);
            item.NetAmount = Round(item.GrossAmount - item.DiscountAmount, decimalPlaces);

            item.TaxAmount = quotation.TaxMode switch
            {
                TaxMode.Exempt => 0,
                TaxMode.Inclusive when item.TaxPercentage > 0 => Round(item.NetAmount - item.NetAmount / (1m + item.TaxPercentage / 100m), decimalPlaces),
                _ => Round(item.NetAmount * item.TaxPercentage / 100m, decimalPlaces)
            };
            item.LineTotal = quotation.TaxMode == TaxMode.Inclusive
                ? item.NetAmount
                : Round(item.NetAmount + item.TaxAmount, decimalPlaces);
        }

        var chargeableItems = quotation.Items.Where(x => x.ItemType != QuotationItemType.SectionHeader).ToList();
        quotation.Subtotal = Round(chargeableItems.Sum(x => x.GrossAmount), decimalPlaces);
        quotation.LineDiscountTotal = Round(chargeableItems.Sum(x => x.DiscountAmount), decimalPlaces);
        var afterLineDiscount = quotation.Subtotal - quotation.LineDiscountTotal;
        quotation.OverallDiscountAmount = quotation.OverallDiscountType == DiscountType.Percentage
            ? Round(afterLineDiscount * ValidatePercentage(quotation.OverallDiscountValue, "Overall discount") / 100m, decimalPlaces)
            : Round(Math.Min(afterLineDiscount, Math.Max(0, quotation.OverallDiscountValue)), decimalPlaces);
        quotation.DiscountTotal = Round(quotation.LineDiscountTotal + quotation.OverallDiscountAmount, decimalPlaces);
        quotation.TaxTotal = Round(chargeableItems.Sum(x => x.TaxAmount), decimalPlaces);
        quotation.NetAmount = quotation.TaxMode == TaxMode.Inclusive
            ? Round(afterLineDiscount - quotation.TaxTotal - quotation.OverallDiscountAmount, decimalPlaces)
            : Round(afterLineDiscount - quotation.OverallDiscountAmount, decimalPlaces);
        quotation.GrandTotal = Round(quotation.NetAmount + quotation.TaxTotal + quotation.ShippingCharge + quotation.AdditionalCharges + quotation.RoundingAdjustment, decimalPlaces);
        if (quotation.GrandTotal < 0) throw new InvalidOperationException("Quotation total cannot be negative.");
        quotation.AmountInWords = MoneyInWords.Format(quotation.GrandTotal, quotation.CurrencyCode);

        return new QuotationTotals(quotation.Subtotal, quotation.DiscountTotal, quotation.NetAmount, quotation.TaxTotal, quotation.GrandTotal);
    }

    private static decimal PercentageDiscount(QuotationItem item, int decimalPlaces)
    {
        var percentage = item.DiscountValue != 0 ? item.DiscountValue : item.DiscountPercentage;
        item.DiscountPercentage = ValidatePercentage(percentage, $"Line {item.LineNumber} discount");
        item.DiscountValue = item.DiscountPercentage;
        return Round(item.GrossAmount * item.DiscountPercentage / 100m, decimalPlaces);
    }

    private static decimal ValidatePercentage(decimal value, string label)
    {
        if (value is < 0 or > 100) throw new InvalidOperationException($"{label} must be between 0 and 100.");
        return value;
    }

    private static decimal Round(decimal value, int places) => Math.Round(value, places, MidpointRounding.AwayFromZero);
}

public sealed record QuotationTotals(decimal Subtotal, decimal Discount, decimal NetBeforeTax, decimal Tax, decimal GrandTotal);

public static class MoneyInWords
{
    public static string Format(decimal amount, string currency)
    {
        var whole = (long)Math.Floor(amount);
        var fraction = (int)Math.Round((amount - whole) * 100m, MidpointRounding.AwayFromZero);
        return fraction == 0
            ? $"{Number(whole)} {currency} only"
            : $"{Number(whole)} {currency} and {Number(fraction)} cents only";
    }

    private static string Number(long number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + Number(Math.Abs(number));
        string[] units = ["", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"];
        string[] tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];
        if (number < 20) return units[number];
        if (number < 100) return tens[number / 10] + (number % 10 == 0 ? "" : "-" + units[number % 10]);
        if (number < 1_000) return units[number / 100] + " Hundred" + (number % 100 == 0 ? "" : " " + Number(number % 100));
        if (number < 1_000_000) return Number(number / 1_000) + " Thousand" + (number % 1_000 == 0 ? "" : " " + Number(number % 1_000));
        if (number < 1_000_000_000) return Number(number / 1_000_000) + " Million" + (number % 1_000_000 == 0 ? "" : " " + Number(number % 1_000_000));
        return Number(number / 1_000_000_000) + " Billion" + (number % 1_000_000_000 == 0 ? "" : " " + Number(number % 1_000_000_000));
    }
}
