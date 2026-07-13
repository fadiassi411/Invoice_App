using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvoiceSoftware.Data;

public sealed class DesignTimeInvoiceDbContextFactory : IDesignTimeDbContextFactory<InvoiceDbContext>
{
    public InvoiceDbContext CreateDbContext(string[] args)
    {
        var designTimeDb = Path.Combine(Directory.GetCurrentDirectory(), "invoice-software-design-time.db");
        var options = new DbContextOptionsBuilder<InvoiceDbContext>()
            .UseSqlite($"Data Source={designTimeDb}")
            .Options;

        return new InvoiceDbContext(options);
    }
}
