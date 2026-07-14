using System.IO.Compression;
using System.Data;
using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace InvoiceSoftware.Services;

public sealed class BackupService(InvoiceDbContext db) : IBackupService
{
    public async Task<string> BackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var backupPath = destinationPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? destinationPath
            : Path.Combine(destinationPath, $"InvoiceSoftwareBackup-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? destinationPath);
        if (File.Exists(backupPath)) throw new InvalidOperationException("Backup file already exists.");

        var company = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var databaseBackupCopy = await CreateDatabaseBackupCopyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(databaseBackupCopy) || !File.Exists(databaseBackupCopy) || new FileInfo(databaseBackupCopy).Length == 0)
            throw new InvalidOperationException("Backup failed because the application database could not be copied.");

        try
        {
            using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
            {
                var addedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                void AddFile(string filePath, string entryName)
                {
                    if (!File.Exists(filePath) || !addedEntries.Add(entryName)) return;
                    archive.CreateEntryFromFile(filePath, entryName);
                }

                if (!string.IsNullOrWhiteSpace(databaseBackupCopy))
                    AddFile(databaseBackupCopy, "invoice-software.db");
                if (!string.IsNullOrWhiteSpace(company?.LogoPath))
                    AddFile(company.LogoPath, $"Assets/{Path.GetFileName(company.LogoPath)}");
                if (!string.IsNullOrWhiteSpace(company?.StampOrSignaturePath))
                    AddFile(company.StampOrSignaturePath, $"Assets/{Path.GetFileName(company.StampOrSignaturePath)}");
                var assets = Path.Combine(DatabasePaths.AppDataFolder, "Assets");
                if (Directory.Exists(assets))
                {
                    foreach (var file in Directory.GetFiles(assets))
                        AddFile(file, $"Assets/{Path.GetFileName(file)}");
                }
            }

            VerifyBackupArchive(backupPath);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(databaseBackupCopy) && File.Exists(databaseBackupCopy))
                File.Delete(databaseBackupCopy);
        }

        if (!File.Exists(backupPath) || new FileInfo(backupPath).Length == 0)
            throw new InvalidOperationException("Backup verification failed.");

        db.Backups.Add(new BackupRecord { FilePath = backupPath, Verified = true });
        await db.SaveChangesAsync(cancellationToken);
        return backupPath;
    }

    private static void VerifyBackupArchive(string backupPath)
    {
        using var archive = ZipFile.OpenRead(backupPath);
        var databaseEntry = archive.GetEntry("invoice-software.db");
        if (databaseEntry is { Length: > 0 }) return;

        archive.Dispose();
        File.Delete(backupPath);
        throw new InvalidOperationException("Backup verification failed because the database was not written into the zip file.");
    }

    public async Task RestoreAsync(string backupFile, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupFile)) throw new InvalidOperationException("Backup file was not found.");
        using var archive = ZipFile.OpenRead(backupFile);
        var databaseEntry = archive.GetEntry("invoice-software.db") ?? throw new InvalidOperationException("The backup does not contain a valid database.");
        var safetyFolder = Path.Combine(DatabasePaths.AppDataFolder, "Backups");
        await BackupAsync(safetyFolder, cancellationToken);
        await db.Database.CloseConnectionAsync();
        SqliteConnection.ClearAllPools();

        Directory.CreateDirectory(DatabasePaths.PendingRestoreFolder);
        databaseEntry.ExtractToFile(DatabasePaths.PendingRestoreDatabaseFile, overwrite: true);
        ExtractAssets(archive);
    }

    private async Task<string?> CreateDatabaseBackupCopyAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var sourceDatabase = Path.GetFullPath(connection.DataSource);
        if (!File.Exists(sourceDatabase)) return null;
        var tempFolder = Path.Combine(Path.GetDirectoryName(sourceDatabase)!, "Temp");
        Directory.CreateDirectory(tempFolder);
        var tempDatabase = Path.Combine(tempFolder, $"invoice-software-{Guid.NewGuid():N}.db");

        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await db.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "VACUUM INTO $backupFile";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$backupFile";
            parameter.Value = tempDatabase;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await db.Database.CloseConnectionAsync();
                SqliteConnection.ClearAllPools();
            }
        }

        return tempDatabase;
    }

    private static void ExtractAssets(ZipArchive archive)
    {
        var assetsFolder = Path.Combine(DatabasePaths.AppDataFolder, "Assets");
        Directory.CreateDirectory(assetsFolder);

        foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)))
        {
            var fileName = Path.GetFileName(entry.FullName);
            if (string.IsNullOrWhiteSpace(fileName)) continue;

            entry.ExtractToFile(Path.Combine(assetsFolder, fileName), overwrite: true);
        }
    }
}
