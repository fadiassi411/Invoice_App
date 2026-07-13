using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Services;

public sealed class LookupService(InvoiceDbContext db) : ILookupService
{
    public async Task<CompanySettings> GetCompanySettingsAsync(CancellationToken cancellationToken = default)
        => await db.CompanySettings.FirstAsync(cancellationToken);

    public async Task SaveCompanySettingsAsync(CompanySettings settings, CancellationToken cancellationToken = default)
    {
        db.CompanySettings.Update(settings);
        await db.AuditLogs.AddAsync(new AuditLog { Action = "Update", EntityName = nameof(CompanySettings), EntityId = settings.Id, Details = "Company settings changed." }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default)
        => db.Customers.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken = default)
        => db.Products.OrderBy(x => x.Description).ToListAsync(cancellationToken);

    public async Task<Customer> AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customer.CustomerCode))
            customer.CustomerCode = await GenerateCustomerCodeAsync(cancellationToken);

        ValidateCustomer(customer);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task SaveCustomerAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        ValidateCustomer(customer);
        db.Customers.Update(customer);
        db.AuditLogs.Add(new AuditLog { Action = "Update", EntityName = nameof(Customer), EntityId = customer.Id, Details = customer.CustomerCode });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCustomerAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken)
            ?? throw new InvalidOperationException("Customer was not found.");

        customer.IsActive = false;
        db.Customers.Update(customer);
        db.AuditLogs.Add(new AuditLog { Action = "Delete", EntityName = nameof(Customer), EntityId = customer.Id, Details = customer.CustomerCode });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Product> AddProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(product.Code)) throw new InvalidOperationException("Product code is required.");
        if (string.IsNullOrWhiteSpace(product.Description)) throw new InvalidOperationException("Product description is required.");
        if (product.SellingPrice < 0) throw new InvalidOperationException("Selling price cannot be negative.");
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return product;
    }

    private static void ValidateCustomer(Customer customer)
    {
        if (string.IsNullOrWhiteSpace(customer.CustomerCode)) throw new InvalidOperationException("Customer code is required.");
        if (string.IsNullOrWhiteSpace(customer.Name)) throw new InvalidOperationException("Customer name is required.");
        if (string.IsNullOrWhiteSpace(customer.AttentionName)) customer.AttentionName = customer.ContactPerson;
        if (!string.IsNullOrWhiteSpace(customer.Email) && !customer.Email.Contains('@')) throw new InvalidOperationException("Customer email address is not valid.");
    }

    private async Task<string> GenerateCustomerCodeAsync(CancellationToken cancellationToken)
    {
        var codes = await db.Customers
            .IgnoreQueryFilters()
            .Select(x => x.CustomerCode)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code) || !code.StartsWith("CUS-", StringComparison.OrdinalIgnoreCase)) continue;
            if (int.TryParse(code[4..], out var number) && number > max) max = number;
        }

        return $"CUS-{max + 1:0000}";
    }
}
