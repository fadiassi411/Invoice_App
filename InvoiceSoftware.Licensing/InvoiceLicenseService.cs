using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace InvoiceSoftware.Licensing;

public sealed record InvoiceLicenseStatus(
    bool IsLicensed,
    string Message,
    string? CustomerName = null,
    string? Edition = null,
    DateTimeOffset? ExpiresAtUtc = null,
    Guid? LicenseId = null);

public interface IInvoiceLicenseService
{
    string InstallationId { get; }
    string LicenseFilePath { get; }
    InvoiceLicenseStatus GetStatus();
    InvoiceLicenseStatus InstallLicense(string sourceFile);
}

public sealed class InvoiceLicenseService : IInvoiceLicenseService
{
    private readonly string _appDataFolder;
    private readonly string _publicKeyPem;
    private readonly Func<DateTimeOffset> _utcNow;

    public InvoiceLicenseService()
        : this(null, null, null)
    {
    }

    public InvoiceLicenseService(
        string? appDataFolder,
        string? publicKeyPem,
        Func<DateTimeOffset>? utcNow)
    {
        _appDataFolder = appDataFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InvoiceSoftware");
        Directory.CreateDirectory(_appDataFolder);
        _publicKeyPem = publicKeyPem ?? ReadEmbeddedPublicKey();
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        InstallationId = CreateInstallationId();
    }

    public string InstallationId { get; }
    public string LicenseFilePath => Path.Combine(_appDataFolder, "invoice-app.invoicelicense");

    public InvoiceLicenseStatus GetStatus()
    {
        if (!File.Exists(LicenseFilePath))
            return new(false, "Invoice Maker is not activated.");

        try
        {
            return Validate(File.ReadAllText(LicenseFilePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(false, $"The installed license could not be read: {ex.Message}");
        }
    }

    public InvoiceLicenseStatus InstallLicense(string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
            throw new FileNotFoundException("Select a valid .invoicelicense file.", sourceFile);

        var json = File.ReadAllText(sourceFile);
        var status = Validate(json);
        if (!status.IsLicensed)
            throw new InvalidDataException(status.Message);

        var temporaryFile = LicenseFilePath + ".tmp";
        File.WriteAllText(temporaryFile, json);
        File.Move(temporaryFile, LicenseFilePath, overwrite: true);
        return status;
    }

    private InvoiceLicenseStatus Validate(string json)
    {
        try
        {
            var document = InvoiceLicenseDocument.Parse(json);
            if (!document.TryVerify(_publicKeyPem, out var payload, out var error) || payload is null)
                return new(false, error);

            if (payload.LicenseId == Guid.Empty || string.IsNullOrWhiteSpace(payload.CustomerName) || string.IsNullOrWhiteSpace(payload.Edition))
                return new(false, "The Invoice App license information is incomplete.");

            if (!string.Equals(payload.InstallationId, InstallationId, StringComparison.OrdinalIgnoreCase))
                return new(false, "This license was created for a different Invoice Maker installation.");

            if (payload.IssuedAtUtc > _utcNow().AddMinutes(5))
                return new(false, "The Invoice App license issue date is in the future.");

            if (payload.ExpiresAtUtc is { } invalidExpiry && invalidExpiry <= payload.IssuedAtUtc)
                return new(false, "The Invoice App license expiry date is invalid.");

            if (payload.ExpiresAtUtc is { } expires && expires < _utcNow())
                return new(false, $"This Invoice App license expired on {expires:yyyy-MM-dd}.", payload.CustomerName, payload.Edition, expires, payload.LicenseId);

            var expiry = payload.ExpiresAtUtc is null ? "Perpetual" : payload.ExpiresAtUtc.Value.ToString("yyyy-MM-dd");
            return new(true, $"Activated for {payload.CustomerName} ({payload.Edition}) - {expiry}", payload.CustomerName, payload.Edition, payload.ExpiresAtUtc, payload.LicenseId);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or FormatException or ArgumentException or CryptographicException)
        {
            return new(false, "The selected file is not a valid Invoice App license.");
        }
    }

    private static string CreateInstallationId()
    {
        string? machineIdentity = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = RegistryKey
                    .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                machineIdentity = key?.GetValue("MachineGuid")?.ToString();
            }
            catch
            {
                // The deterministic machine fallback below keeps activation usable on restricted systems.
            }
        }

        machineIdentity ??= $"{Environment.MachineName}|{Environment.OSVersion.Platform}|{Environment.Is64BitOperatingSystem}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"MicroBrain.InvoiceMaker|{machineIdentity.Trim()}"));
        var hex = Convert.ToHexString(hash[..15]);
        var groups = Enumerable.Range(0, 6).Select(index => hex.Substring(index * 5, 5));
        return "INV-" + string.Join('-', groups);
    }

    private static string ReadEmbeddedPublicKey()
    {
        var assembly = typeof(InvoiceLicenseService).Assembly;
        const string resourceName = "InvoiceSoftware.Licensing.invoice-license-public.pem";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The Invoice App public license key is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
