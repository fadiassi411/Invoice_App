using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Services;

public sealed class DashboardService(InvoiceDbContext db) : IDashboardService
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var invoices = db.Invoices.AsNoTracking();
        var receipts = db.Receipts.AsNoTracking();
        var monthlySales = (await invoices.Where(x => x.InvoiceDate >= monthStart).Select(x => x.GrandTotal).ToListAsync(cancellationToken)).Sum();
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
            await invoices.Include(x => x.Customer).OrderByDescending(x => x.InvoiceDate).Take(8).ToListAsync(cancellationToken),
            await receipts.Include(x => x.Customer).OrderByDescending(x => x.ReceiptDate).Take(8).ToListAsync(cancellationToken),
            activeProducts,
            quantities.Sum(x => x.CurrentQuantity),
            quantities.Sum(x => x.CurrentQuantity * x.CostPrice),
            await products.CountAsync(x => x.TrackStock && x.CurrentQuantity > 0 && x.CurrentQuantity <= x.MinimumQuantity, cancellationToken),
            await products.CountAsync(x => x.TrackStock && x.CurrentQuantity <= 0, cancellationToken),
            todayItems.Sum(x => x.Quantity * x.UnitPrice),
            todayItems.Sum(x => x.Quantity * (x.UnitPrice - x.CostPriceSnapshot)));
    }
}
