using InvoiceSoftware.Core.Models;

namespace InvoiceSoftware.Core.Services;

/// <summary>Internal-only gross profit estimate, deliberately excluded from customer documents.</summary>
public static class ProfitEstimateCalculator
{
    public static ProfitEstimate ForQuotation(Quotation quotation)
    {
        var lines = quotation.Items.Where(item => item.ItemType != QuotationItemType.SectionHeader).ToList();
        var missing = lines.Count(item => item.CostPriceSnapshot is null or < 0m);
        var cost = Round(lines.Sum(item => item.Quantity * Math.Max(0m, item.CostPriceSnapshot ?? 0m)));
        return Create(quotation.NetAmount, cost, missing);
    }

    public static ProfitEstimate ForInvoice(Invoice invoice)
    {
        var missing = invoice.Items.Count(item => item.CostPriceSnapshot <= 0m);
        var cost = Round(invoice.Items.Sum(item => item.Quantity * Math.Max(0m, item.CostPriceSnapshot)));
        return Create(invoice.TotalBeforeTax, cost, missing);
    }

    private static ProfitEstimate Create(decimal sales, decimal cost, int missing)
    {
        var profit = missing == 0 ? Round(sales - cost) : (decimal?)null;
        var margin = profit is not null && sales > 0m ? Round(profit.Value * 100m / sales) : (decimal?)null;
        return new ProfitEstimate(Round(sales), cost, profit, margin, missing);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record ProfitEstimate(decimal NetSales, decimal KnownCost, decimal? GrossProfit, decimal? MarginPercent, int MissingCostLines)
{
    public static ProfitEstimate Empty { get; } = new(0, 0, null, null, 0);
    public string ProfitDisplay => GrossProfit?.ToString("N2") ?? "—";
    public string MarginDisplay => MarginPercent is null ? "—" : $"{MarginPercent:N2}%";
    public string Warning => MissingCostLines > 0
        ? $"Cost missing on {MissingCostLines} line(s). Enter internal unit costs to see a reliable estimate."
        : "Gross profit excludes tax, delivery costs, overhead and other expenses.";
}
