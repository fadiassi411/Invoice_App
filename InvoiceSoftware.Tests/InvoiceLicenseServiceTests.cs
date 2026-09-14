using System.Security.Cryptography;
using System.Text.Json;
using InvoiceSoftware.Licensing;

namespace InvoiceSoftware.Tests;

public sealed class InvoiceLicenseServiceTests
{
    [Fact]
    public void Installation_id_is_valid_and_persists()
    {
        using var fixture = new LicenseFixture();
        var first = fixture.CreateService();
        var second = fixture.CreateService();

        Assert.Matches(@"^INV-(?:[0-9A-F]{5}-){5}[0-9A-F]{5}$", first.InstallationId);
        Assert.Equal(first.InstallationId, second.InstallationId);
    }

    [Fact]
    public void Matching_signed_license_activates_application()
    {
        using var fixture = new LicenseFixture();
        var service = fixture.CreateService();
        var file = fixture.CreateLicense(service.InstallationId);

        var installed = service.InstallLicense(file);

        Assert.True(installed.IsLicensed);
        Assert.True(service.GetStatus().IsLicensed);
        Assert.Equal("Test Customer", service.GetStatus().CustomerName);
    }

    [Fact]
    public void License_for_another_installation_is_rejected()
    {
        using var fixture = new LicenseFixture();
        var service = fixture.CreateService();
        var file = fixture.CreateLicense("INV-ABCDE-12345-67890-ABCDE-12345-67890");

        var error = Assert.Throws<InvalidDataException>(() => service.InstallLicense(file));

        Assert.Contains("different", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Expired_license_is_rejected()
    {
        using var fixture = new LicenseFixture();
        var service = fixture.CreateService();
        var file = fixture.CreateLicense(service.InstallationId, fixture.Now.AddDays(-1));

        var error = Assert.Throws<InvalidDataException>(() => service.InstallLicense(file));

        Assert.Contains("expired", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tampered_license_is_rejected()
    {
        using var fixture = new LicenseFixture();
        var service = fixture.CreateService();
        var file = fixture.CreateLicense(service.InstallationId);
        var document = JsonSerializer.Deserialize<InvoiceLicenseDocument>(File.ReadAllText(file), LicenseFixture.JsonOptions)!;
        var first = document.Payload[0] == 'A' ? 'B' : 'A';
        File.WriteAllText(file, JsonSerializer.Serialize(document with { Payload = first + document.Payload[1..] }, LicenseFixture.JsonOptions));

        var error = Assert.Throws<InvalidDataException>(() => service.InstallLicense(file));

        Assert.Contains("signature", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class LicenseFixture : IDisposable
    {
        internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        public LicenseFixture()
        {
            Folder = Path.Combine(Path.GetTempPath(), "invoice-license-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
        }

        public string Folder { get; }
        public DateTimeOffset Now { get; } = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

        public InvoiceLicenseService CreateService() => new(Folder, _key.ExportSubjectPublicKeyInfoPem(), () => Now);

        public string CreateLicense(string installationId, DateTimeOffset? expiry = null)
        {
            var issuedAt = expiry is null ? Now.AddDays(-1) : expiry.Value.AddDays(-30);
            var payload = new InvoiceLicensePayload(3, "InvoiceApp", Guid.NewGuid(), "Test Customer", installationId, "Professional", issuedAt, expiry);
            var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
            var signature = _key.SignData(payloadBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            var document = new InvoiceLicenseDocument(Encode(payloadBytes), Encode(signature));
            var file = Path.Combine(Folder, Guid.NewGuid() + ".invoicelicense");
            File.WriteAllText(file, JsonSerializer.Serialize(document, JsonOptions));
            return file;
        }

        public void Dispose()
        {
            _key.Dispose();
            if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
        }

        private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
