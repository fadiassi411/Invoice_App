using InvoiceSoftware.Data;
using Microsoft.Data.Sqlite;

var databaseFile = DatabasePaths.DatabaseFile;
if (!File.Exists(databaseFile))
{
    Console.WriteLine($"Database not found: {databaseFile}");
    return 1;
}

var outputsFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "outputs"));
Directory.CreateDirectory(outputsFolder);
var backupPath = Path.Combine(outputsFolder, $"invoice-software-before-history-reset-{DateTime.Now:yyyyMMdd-HHmmss}.db");
File.Copy(databaseFile, backupPath, overwrite: false);

await using var connection = new SqliteConnection($"Data Source={databaseFile}");
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();

async Task ExecuteAsync(string sql)
{
    await using var command = connection.CreateCommand();
    command.Transaction = (SqliteTransaction)transaction;
    command.CommandText = sql;
    await command.ExecuteNonQueryAsync();
}

await ExecuteAsync("DELETE FROM ReceiptPayments;");
await ExecuteAsync("DELETE FROM Receipts;");
await ExecuteAsync("DELETE FROM InvoiceItems;");
await ExecuteAsync("DELETE FROM Invoices;");
await ExecuteAsync("DELETE FROM AuditLogs WHERE EntityName IN ('Invoice', 'InvoiceItem', 'Receipt', 'ReceiptPayment');");
await ExecuteAsync("DELETE FROM DocumentSequences WHERE Kind IN (0, 1);");
await ExecuteAsync("DELETE FROM sqlite_sequence WHERE name IN ('Invoices', 'InvoiceItems', 'Receipts', 'ReceiptPayments', 'DocumentSequences');");

await transaction.CommitAsync();

Console.WriteLine("Invoice and receipt history cleared.");
Console.WriteLine("Invoice and receipt counters reset.");
Console.WriteLine($"Safety backup: {backupPath}");
return 0;
