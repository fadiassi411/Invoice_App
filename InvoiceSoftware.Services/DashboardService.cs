using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace InvoiceSoftware.Services;

public sealed class DashboardService(InvoiceDbContext db) : IDashboardService
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var historyStart = new DateTime(today.Year - 4, 1, 1);
        var tomorrow = today.AddDays(1);
        var invoices = db.Invoices.AsNoTracking();
        var receipts = db.Receipts.AsNoTracking();
        var finalizedSales = await invoices
            .Where(x => x.Status == InvoiceStatus.Finalized && x.InvoiceDate >= historyStart && x.InvoiceDate < tomorrow)
            .Select(x => new { x.InvoiceDate, x.GrandTotal })
            .ToListAsync(cancellationToken);
        var monthlySales = finalizedSales.Where(x => x.InvoiceDate >= monthStart).Sum(x => x.GrandTotal);
        var monthlyTrend = ScaleChart(Enumerable.Range(0, 12).Select(index =>
        {
            var start = monthStart.AddMonths(index - 11);
            var end = start.AddMonths(1);
            return (start.ToString("MMM", CultureInfo.CurrentCulture), start.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
                finalizedSales.Where(x => x.InvoiceDate >= start && x.InvoiceDate < end).Sum(x => x.GrandTotal));
        }));
        var yearlyTrend = ScaleChart(Enumerable.Range(0, 5).Select(index =>
        {
            var year = today.Year - 4 + index;
            return (year.ToString(CultureInfo.InvariantCulture), year.ToString(CultureInfo.InvariantCulture),
                finalizedSales.Where(x => x.InvoiceDate.Year == year).Sum(x => x.GrandTotal));
        }));
        var outstandingAmount = (await invoices.Select(x => x.RemainingBalance).ToListAsync(cancellationToken)).Sum();
        var paymentsReceived = (await receipts.Select(x => x.PaymentAmount).ToListAsync(cancellationToken)).Sum();
        var products = db.Products.AsNoTracking().Where(x => x.IsActive);
        var activeProducts = await products.CountAsync(cancellationToken);
        var quantities = await products.Where(x => x.TrackStock).Select(x => new { x.CurrentQuantity, x.CostPrice }).ToListAsync(cancellationToken);
        var todayItems = await db.InvoiceItems.AsNoTracking()
            .Where(x => x.Invoice != null && x.Invoice.Status == InvoiceStatus.Finalized && x.Invoice.InvoiceDate.Date == today)
            .Select(x => new { x.Quantity, x.UnitPrice, x.CostPriceSnapshot }).ToListAsync(cancellationToken);

        return new DashboardSnapshot(
            await invoices.CountAsync(cancellationToken),
            await invoices.CountAsync(x => x.InvoiceDate.Date == today, cancellationToken),
            monthlySales,
            await invoices.CountAsync(x => x.PaymentStatus == PaymentStatus.Paid, cancellationToken),
            await invoices.CountAsync(x => x.PaymentStatus == PaymentStatus.PartiallyPaid, cancellationToken),
            await invoices.CountAsync(x => x.PaymentStatus == PaymentStatus.Unpaid, cancellationToken),
            await invoices.CountAsync(x => x.DueDate < today && x.PaymentStatus != PaymentStatus.Paid, cancellationToken),
            outstandingAmount,
            paymentsReceived,
            activeProducts,
            quantities.Sum(x => x.CurrentQuantity),
            quantities.Sum(x => x.CurrentQuantity * x.CostPrice),
            await products.CountAsync(x => x.TrackStock && x.CurrentQuantity > 0 && x.CurrentQuantity <= x.MinimumQuantity, cancellationToken),
            await products.CountAsync(x => x.TrackStock && x.CurrentQuantity <= 0, cancellationToken),
            todayItems.Sum(x => x.Quantity * x.UnitPrice),
            todayItems.Sum(x => x.Quantity * (x.UnitPrice - x.CostPriceSnapshot)),
            monthlyTrend,
            yearlyTrend);
    }

    private static IReadOnlyList<DashboardSalesPoint> ScaleChart(
        IEnumerable<(string Label, string Period, decimal Amount)> values)
    {
        var items = values.ToList();
        var maximum = items.Count == 0 ? 0 : items.Max(x => x.Amount);
        return items.Select(x => new DashboardSalesPoint(
            x.Label,
            x.Period,
            x.Amount,
            maximum <= 0 ? 4d : Math.Max(4d, (double)(x.Amount / maximum) * 112d))).ToList();
    }
}
