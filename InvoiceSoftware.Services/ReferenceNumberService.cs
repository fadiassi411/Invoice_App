using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace InvoiceSoftware.Services;

public sealed class ReferenceNumberService(InvoiceDbContext db) : IReferenceNumberService
{
    public async Task<string> GenerateAsync(SequenceKind kind, CancellationToken cancellationToken = default)
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        var settings = await db.CompanySettings.FirstAsync(cancellationToken);
        var prefix = kind switch
        {
            SequenceKind.Invoice => settings.InvoicePrefix,
            SequenceKind.Receipt => settings.ReceiptPrefix,
            SequenceKind.Quotation => settings.QuotationPrefix,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var displayYear = DateTime.Today.Year;
        var resetYearly = kind != SequenceKind.Quotation || settings.ResetQuotationSequenceYearly;
        var sequenceYear = settings.IncludeYearInReferences && resetYearly ? displayYear : 0;
        var startingNumber = kind == SequenceKind.Quotation ? Math.Max(1, settings.QuotationStartingSequence) : 1;

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            INSERT INTO DocumentSequences (Kind, Prefix, Year, LastNumber, CreatedAt, IsDeleted)
            VALUES ($kind, $prefix, $year, $startingNumber, CURRENT_TIMESTAMP, 0)
            ON CONFLICT(Kind, Prefix, Year) DO UPDATE SET
                LastNumber = LastNumber + 1,
                ModifiedAt = CURRENT_TIMESTAMP
            RETURNING LastNumber;
            """;
        AddParameter(command, "$kind", (int)kind);
        AddParameter(command, "$prefix", prefix);
        AddParameter(command, "$year", sequenceYear);
        AddParameter(command, "$startingNumber", startingNumber);
        var number = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        return settings.IncludeYearInReferences
            ? $"{prefix}-{displayYear}-{number:000000}"
            : $"{prefix}-{number:000000}";
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
