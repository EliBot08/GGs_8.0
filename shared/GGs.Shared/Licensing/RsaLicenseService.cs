using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GGs.Shared.Licensing;

public static class RsaLicenseService
{
    public static string ComputePublicKeyFingerprint(string publicKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        var der = rsa.ExportSubjectPublicKeyInfo();
        var sha = SHA256.HashData(der);
        return Convert.ToHexString(sha);
    }

    public static string CanonicalJson<T>(T obj)
    {
        // deterministic JSON for signing
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        return JsonSerializer.Serialize(obj, opts);
    }

    public static SignedLicense Sign(LicensePayload payload, string privateKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var canonical = CanonicalJson(payload);
        var data = Encoding.UTF8.GetBytes(canonical);
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var pubPem = ExportPublicPem(rsa);
        return new SignedLicense
        {
            Payload = payload,
            Signature = Convert.ToBase64String(signature),
            KeyFingerprint = ComputePublicKeyFingerprint(pubPem)
        };
    }

    public static bool Verify(SignedLicense signed, string publicKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        var canonical = CanonicalJson(signed.Payload);
        var data = Encoding.UTF8.GetBytes(canonical);
        var sig = Convert.FromBase64String(signed.Signature);
        return rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    // Generic payload signing for arbitrary models (e.g., CloudProfilePayload)
    public static (string signature, string keyFingerprint) SignPayload<T>(T payload, string privateKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var canonical = CanonicalJson(payload);
        var data = Encoding.UTF8.GetBytes(canonical);
        var sigBytes = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var pubPem = ExportPublicPem(rsa);
        return (Convert.ToBase64String(sigBytes), ComputePublicKeyFingerprint(pubPem));
    }

    public static bool VerifyPayload<T>(T payload, string signatureBase64, string publicKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        var canonical = CanonicalJson(payload);
        var data = Encoding.UTF8.GetBytes(canonical);
        var sig = Convert.FromBase64String(signatureBase64);
        return rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    public static bool IsExpired(LicensePayload payload, DateTime utcNow)
        => payload.ExpiresUtc.HasValue && payload.ExpiresUtc.Value <= utcNow;

    public static bool IsDeviceMatch(LicensePayload payload, string? currentDeviceBinding)
    {
        if (string.IsNullOrWhiteSpace(payload.DeviceBindingId)) return true; // not bound
        if (string.IsNullOrWhiteSpace(currentDeviceBinding)) return false;
        return string.Equals(payload.DeviceBindingId, currentDeviceBinding, StringComparison.OrdinalIgnoreCase);
    }

    public static (string privatePem, string publicPem) CreateRsaKeyPair(int keySize = 2048)
    {
        using var rsa = RSA.Create(keySize);
        var privatePem = ExportPrivatePem(rsa);
        var publicPem = ExportPublicPem(rsa);
        return (privatePem, publicPem);
    }

    private static string ExportPrivatePem(RSA rsa)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PRIVATE KEY-----");
        sb.AppendLine(Convert.ToBase64String(rsa.ExportPkcs8PrivateKey(), Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END PRIVATE KEY-----");
        return sb.ToString();
    }

    public static string ExportPublicPem(RSA rsa)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PUBLIC KEY-----");
        sb.AppendLine(Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo(), Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END PUBLIC KEY-----");
        return sb.ToString();
    }
}
