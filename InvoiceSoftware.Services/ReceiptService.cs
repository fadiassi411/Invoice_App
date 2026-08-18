using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Core.Services;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Services;

public sealed class ReceiptService(InvoiceDbContext db, IReferenceNumberService references, InvoiceCalculator calculator) : IReceiptService
{
    public Task<List<Receipt>> GetReceiptsAsync(CancellationToken cancellationToken = default)
        => db.Receipts.Include(x => x.Customer).Include(x => x.Invoice).OrderByDescending(x => x.ReceiptDate).ThenByDescending(x => x.Id).Take(250).ToListAsync(cancellationToken);

    public async Task<Receipt> GetOrCreateForInvoicePdfAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var existing = await db.Receipts
            .Include(x => x.Customer)
            .Include(x => x.Invoice)
            .Where(x => x.InvoiceId == invoiceId)
            .OrderByDescending(x => x.ReceiptDate)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;

        throw new InvalidOperationException("No receipt exists for this invoice yet. Press Create Receipt first and enter the payment amount.");
    }

    public async Task<Receipt> CreateForInvoiceAsync(int invoiceId, decimal amount, PaymentMethodType paymentMethod, string? transactionReference, string? notes = null, CancellationToken cancellationToken = default)
    {
        if (amount <= 0) throw new InvalidOperationException("Payment amount must be greater than zero.");
        var settings = await db.CompanySettings.FirstAsync(cancellationToken);
        var invoice = await db.Invoices.Include(x => x.Items).FirstAsync(x => x.Id == invoiceId, cancellationToken);
        calculator.Calculate(invoice);
        if (!settings.AllowOverpayments && amount > invoice.RemainingBalance)
            throw new InvalidOperationException("Payment cannot be greater than the remaining balance.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var receipt = new Receipt
        {
            ReferenceNumber = await references.GenerateAsync(SequenceKind.Receipt, cancellationToken),
            CustomerId = invoice.CustomerId,
            InvoiceId = invoice.Id,
            PaymentAmount = amount,
            PaymentMethod = paymentMethod,
            TransactionReference = transactionReference,
            Notes = notes,
            ReceivedBy = settings.ContactPersonName
        };

        invoice.AmountPaid += amount;
        calculator.Calculate(invoice);
        db.Receipts.Add(receipt);
        db.AuditLogs.Add(new AuditLog { Action = "CreateReceipt", EntityName = nameof(Receipt), Details = $"{receipt.ReferenceNumber} for {invoice.ReferenceNumber}" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        receipt.Invoice = invoice;
        receipt.Customer = await db.Customers.FindAsync([invoice.CustomerId], cancellationToken);
        return receipt;
    }

    public async Task<Receipt> CreateForRecordedPaymentAsync(int invoiceId, PaymentMethodType paymentMethod, string? transactionReference, string? notes = null, CancellationToken cancellationToken = default)
    {
        var settings = await db.CompanySettings.FirstAsync(cancellationToken);
        var invoice = await db.Invoices.Include(x => x.Items).Include(x => x.Receipts).FirstAsync(x => x.Id == invoiceId, cancellationToken);
        calculator.Calculate(invoice);
        if (invoice.AmountPaid <= 0 || invoice.RemainingBalance > 0)
            throw new InvalidOperationException("A recorded-payment receipt can only be created for a fully paid invoice.");
        if (invoice.Receipts.Count != 0)
            throw new InvalidOperationException("A receipt already exists for this invoice.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var receipt = new Receipt
        {
            ReferenceNumber = await references.GenerateAsync(SequenceKind.Receipt, cancellationToken),
            CustomerId = invoice.CustomerId,
            InvoiceId = invoice.Id,
            PaymentAmount = invoice.AmountPaid,
            PaymentMethod = paymentMethod,
            TransactionReference = transactionReference,
            Notes = notes,
            ReceivedBy = settings.ContactPersonName
        };

        db.Receipts.Add(receipt);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "CreateReceiptForRecordedPayment",
            EntityName = nameof(Receipt),
            Details = $"{receipt.ReferenceNumber} for already-paid invoice {invoice.ReferenceNumber}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        receipt.Invoice = invoice;
        receipt.Customer = await db.Customers.FindAsync([invoice.CustomerId], cancellationToken);
        return receipt;
    }

    public async Task UpdateAsync(int receiptId, DateTime receiptDate, PaymentMethodType paymentMethod, string? transactionReference,
        string? notes, string? receivedBy, CancellationToken cancellationToken = default)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(x => x.Id == receiptId, cancellationToken)
            ?? throw new InvalidOperationException("Receipt was not found.");

        receipt.ReceiptDate = receiptDate.Date;
        receipt.PaymentMethod = paymentMethod;
        receipt.TransactionReference = Clean(transactionReference);
        receipt.Notes = Clean(notes);
        receipt.ReceivedBy = Clean(receivedBy);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "Modify",
            EntityName = nameof(Receipt),
            EntityId = receipt.Id,
            Details = receipt.ReferenceNumber
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(int receiptId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var receipt = await db.Receipts.FirstOrDefaultAsync(x => x.Id == receiptId, cancellationToken)
            ?? throw new InvalidOperationException("Receipt was not found.");

        if (receipt.InvoiceId is int invoiceId && await AppliesToInvoiceBalanceAsync(receipt, cancellationToken))
        {
            var invoice = await db.Invoices.IgnoreQueryFilters().Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);
            if (invoice is not null)
            {
                invoice.AmountPaid = Math.Max(0m, invoice.AmountPaid - receipt.PaymentAmount);
                calculator.Calculate(invoice);
            }
        }

        receipt.IsDeleted = true;
        receipt.DeletedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            Action = "SoftDelete",
            EntityName = nameof(Receipt),
            EntityId = receipt.Id,
            Details = receipt.ReferenceNumber
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<bool> AppliesToInvoiceBalanceAsync(Receipt receipt, CancellationToken cancellationToken)
        => db.AuditLogs.AnyAsync(x => x.Action == "CreateReceipt"
            && x.Details != null
            && x.Details.StartsWith(receipt.ReferenceNumber + " for "), cancellationToken);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
