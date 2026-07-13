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
            await receipts.Include(x => x.Customer).OrderByDescending(x => x.ReceiptDate).Take(8).ToListAsync(cancellationToken));
    }
}
