using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace GGs.Desktop.Services;

public static class DeviceEnrollmentService
{
    private sealed class DeviceMeta { public string? Thumbprint { get; set; } public DateTime EnrolledUtc { get; set; } }

    public static async Task EnsureEnrolledAsync()
    {
        try
        {
            var deviceId = GGs.Shared.Platform.DeviceIdHelper.GetStableDeviceId();
            // Generate or find a self-signed cert in CurrentUser/My
            var cert = FindExisting(deviceId) ?? CreateSelfSigned(deviceId);

            // Enable client cert usage via env for subsequent service constructions
            Environment.SetEnvironmentVariable("Security:Http:ClientCertificate:Enabled", "true");
            Environment.SetEnvironmentVariable("Security:Http:ClientCertificate:FindType", "FindByThumbprint");
            Environment.SetEnvironmentVariable("Security:Http:ClientCertificate:FindValue", cert.Thumbprint);

            // Call server enroll
            var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
            var baseUrl = cfg["Server:BaseUrl"] ?? "https://localhost:5001";
            var http = GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(baseUrl, new GGs.Shared.Http.HttpClientSecurityOptions(), userAgent: "GGs.Desktop");
            var payload = new { deviceId, thumbprint = cert.Thumbprint, commonName = cert.Subject, certificateDerBase64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert)) };
            using var resp = await http.PostAsJsonAsync("api/devices/enroll", payload);
            resp.EnsureSuccessStatusCode();

            // Persist meta
            SaveMeta(cert.Thumbprint);
        }
        catch { }
    }

    private static void SaveMeta(string thumbprint)
    {
        try
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(baseDir, "GGs", "device"); Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "device.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new DeviceMeta { Thumbprint = thumbprint, EnrolledUtc = DateTime.UtcNow }));
        }
        catch { }
    }

    private static X509Certificate2? FindExisting(string deviceId)
    {
        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (var c in store.Certificates)
            {
                if (c.Subject.Contains(deviceId, StringComparison.OrdinalIgnoreCase) && IsUsable(c))
                    return c;
            }
        }
        catch { }
        return null;
    }

    private static bool IsUsable(X509Certificate2 c)
    {
        try { return DateTime.UtcNow >= c.NotBefore.ToUniversalTime() && DateTime.UtcNow <= c.NotAfter.ToUniversalTime(); } catch { return false; }
    }

    private static X509Certificate2 CreateSelfSigned(string deviceId)
    {
        var name = new X500DistinguishedName($"CN=GGs Device {deviceId}");
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(3));
        // Persist to CurrentUser\My
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(cert);
        return cert;
    }
}
