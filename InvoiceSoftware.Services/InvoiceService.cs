using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Services;

public sealed class InvoiceService(InvoiceDbContext db, IReferenceNumberService references, InvoiceCalculator calculator) : IInvoiceService
{
    public async Task<Invoice> CreateDraftAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var company = await db.CompanySettings.FirstAsync(cancellationToken);
        var customer = await db.Customers.FindAsync([customerId], cancellationToken) ?? throw new InvalidOperationException("Customer was not found.");
        var reference = await references.GenerateAsync(SequenceKind.Invoice, cancellationToken);
        return new Invoice
        {
            ReferenceNumber = reference,
            InvoiceNumber = reference,
            CustomerId = customer.Id,
            Customer = customer,
            CustomerNameSnapshot = customer.Name,
            CustomerAttentionNameSnapshot = customer.AttentionName,
            CustomerAddressSnapshot = customer.Address,
            CustomerTelephoneSnapshot = customer.Telephone,
            CustomerTaxNumberSnapshot = customer.TaxNumber,
            Currency = company.Currency,
            PreviousBalance = customer.OpeningBalance
        };
    }

    public async Task SaveAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice.DueDate < invoice.InvoiceDate) throw new InvalidOperationException("Due date cannot be earlier than invoice date.");
        if (invoice.Items.Count == 0) throw new InvalidOperationException("Add at least one invoice item.");
        calculator.Calculate(invoice);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (invoice.Id == 0)
        {
            if (await db.Invoices.AnyAsync(x => x.ReferenceNumber == invoice.ReferenceNumber, cancellationToken))
                throw new InvalidOperationException("Invoice reference already exists.");
            db.Invoices.Add(invoice);
            db.AuditLogs.Add(new AuditLog { Action = "Create", EntityName = nameof(Invoice), Details = invoice.ReferenceNumber });
        }
        else
        {
            db.Invoices.Update(invoice);
            db.AuditLogs.Add(new AuditLog { Action = "Modify", EntityName = nameof(Invoice), EntityId = invoice.Id, Details = invoice.ReferenceNumber });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<List<Invoice>> SearchAsync(string? text, InvoiceStatus? status, PaymentStatus? paymentStatus, CancellationToken cancellationToken = default)
    {
        var query = db.Invoices.Include(x => x.Customer).Include(x => x.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(text))
        {
            query = query.Where(x => x.ReferenceNumber.Contains(text) || (x.CustomerNameSnapshot ?? "").Contains(text) || (x.Customer != null && x.Customer.Name.Contains(text)));
        }
        if (status is not null) query = query.Where(x => x.Status == status);
        if (paymentStatus is not null) query = query.Where(x => x.PaymentStatus == paymentStatus);
        return query.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.Id).Take(250).ToListAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.FindAsync([invoiceId], cancellationToken) ?? throw new InvalidOperationException("Invoice was not found.");
        invoice.IsDeleted = true;
        invoice.DeletedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { Action = "SoftDelete", EntityName = nameof(Invoice), EntityId = invoice.Id, Details = invoice.ReferenceNumber });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPaidAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.Include(x => x.Items).FirstAsync(x => x.Id == invoiceId, cancellationToken);
        calculator.Calculate(invoice);
        invoice.AmountPaid = invoice.GrandTotal;
        calculator.Calculate(invoice);
        db.AuditLogs.Add(new AuditLog { Action = "MarkPaid", EntityName = nameof(Invoice), EntityId = invoice.Id, Details = invoice.ReferenceNumber });
        await db.SaveChangesAsync(cancellationToken);
    }
}
