using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Services;

public sealed class ReferenceNumberService(InvoiceDbContext db) : IReferenceNumberService
{
    public async Task<string> GenerateAsync(SequenceKind kind, CancellationToken cancellationToken = default)
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var settings = await db.CompanySettings.FirstAsync(cancellationToken);
        var prefix = kind == SequenceKind.Invoice ? settings.InvoicePrefix : settings.ReceiptPrefix;
        var year = settings.IncludeYearInReferences ? DateTime.Today.Year : 0;
        var sequence = await db.DocumentSequences.SingleOrDefaultAsync(x => x.Kind == kind && x.Prefix == prefix && x.Year == year, cancellationToken);
        if (sequence is null)
        {
            sequence = new DocumentSequence { Kind = kind, Prefix = prefix, Year = year };
            db.DocumentSequences.Add(sequence);
        }

        sequence.LastNumber++;
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        return settings.IncludeYearInReferences
            ? $"{prefix}-{year}-{sequence.LastNumber:000000}"
            : $"{prefix}-{sequence.LastNumber:000000}";
    }
}
