using System.Security.Cryptography;
using System.Text.Json;

namespace InvoiceSoftware.Licensing;

public sealed record InvoiceLicensePayload(
    int FormatVersion,
    string Product,
    Guid LicenseId,
    string CustomerName,
    string InstallationId,
    string Edition,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc);

public sealed record InvoiceLicenseDocument(string Payload, string Signature)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    public static InvoiceLicenseDocument Parse(string json) =>
        JsonSerializer.Deserialize<InvoiceLicenseDocument>(json, Options)
        ?? throw new InvalidDataException("Invalid Invoice App license file.");

    public bool TryVerify(string publicKeyPem, out InvoiceLicensePayload? payload, out string error)
    {
        payload = null;
        error = "Invalid Invoice App license signature, product, or format.";

        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(publicKeyPem);
            var payloadBytes = Decode(Payload);
            if (!key.VerifyData(
                    payloadBytes,
                    Decode(Signature),
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
            {
                return false;
            }

            var candidate = JsonSerializer.Deserialize<InvoiceLicensePayload>(payloadBytes, Options);
            if (candidate?.FormatVersion != 3 || candidate.Product != "InvoiceApp")
            {
                return false;
            }

            payload = candidate;
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException or CryptographicException)
        {
            return false;
        }
    }
}
