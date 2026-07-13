namespace InvoiceSoftware.Data;

public static class DatabasePaths
{
    public static string AppDataFolder
    {
        get
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvoiceSoftware");
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(Path.Combine(folder, "Backups"));
            Directory.CreateDirectory(Path.Combine(folder, "Assets"));
            Directory.CreateDirectory(Path.Combine(folder, "PendingRestore"));
            return folder;
        }
    }

    public static string DatabaseFile => Path.Combine(AppDataFolder, "invoice-software.db");
    public static string PendingRestoreFolder => Path.Combine(AppDataFolder, "PendingRestore");
    public static string PendingRestoreDatabaseFile => Path.Combine(PendingRestoreFolder, "invoice-software.db");
    public static string ConnectionString => $"Data Source={DatabaseFile}";
}
