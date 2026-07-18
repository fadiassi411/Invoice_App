using System.Globalization;
using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using InvoiceSoftware.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Tests;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task Sales_charts_include_finalized_invoices_and_fill_missing_periods()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new InvoiceDbContext(new DbContextOptionsBuilder<InvoiceDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await db.Invoices.ExecuteDeleteAsync();

        var today = DateTime.Today;
        var finalized = new[]
        {
            Invoice("DASH-CURRENT", today, 100m, InvoiceStatus.Finalized),
            Invoice("DASH-PREVIOUS", today.AddMonths(-1), 50m, InvoiceStatus.Finalized),
            Invoice("DASH-LAST-YEAR", today.AddYears(-1), 200m, InvoiceStatus.Finalized)
        };
        db.Invoices.AddRange(finalized);
        db.Invoices.Add(Invoice("DASH-DRAFT", today, 999m, InvoiceStatus.Draft));
        await db.SaveChangesAsync();

        var snapshot = await new DashboardService(db).GetSnapshotAsync();

        Assert.Equal(12, snapshot.MonthlySalesTrend.Count);
        Assert.Equal(5, snapshot.YearlySalesTrend.Count);
        Assert.Equal(100m, snapshot.MonthlySales);
        Assert.Equal(100m, snapshot.MonthlySalesTrend.Single(x => x.Period == today.ToString("MMMM yyyy", CultureInfo.CurrentCulture)).Amount);
        Assert.Equal(50m, snapshot.MonthlySalesTrend.Single(x => x.Period == today.AddMonths(-1).ToString("MMMM yyyy", CultureInfo.CurrentCulture)).Amount);

        var expectedCurrentYear = finalized.Where(x => x.InvoiceDate.Year == today.Year).Sum(x => x.GrandTotal);
        Assert.Equal(expectedCurrentYear, snapshot.YearlySalesTrend.Single(x => x.Label == today.Year.ToString(CultureInfo.InvariantCulture)).Amount);
        Assert.All(snapshot.MonthlySalesTrend.Concat(snapshot.YearlySalesTrend), point => Assert.InRange(point.BarHeight, 4d, 112d));
    }

    private static Invoice Invoice(string reference, DateTime date, decimal total, InvoiceStatus status) => new()
    {
        ReferenceNumber = reference,
        InvoiceNumber = reference,
        InvoiceDate = date,
        DueDate = date.AddDays(30),
        CustomerId = 1,
        CustomerNameSnapshot = "Dashboard Customer",
        Status = status,
        PaymentStatus = PaymentStatus.Unpaid,
        GrandTotal = total,
        RemainingBalance = total
    };
}
