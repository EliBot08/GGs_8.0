using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Net.Http.Json;

namespace GGs.Agent;

public static class DeviceEnrollmentService
{
    public static void EnsureEnrolled()
    {
        try
        {
            var deviceId = GGs.Shared.Platform.DeviceIdHelper.GetStableDeviceId();
            var cert = FindExisting(deviceId) ?? CreateSelfSigned(deviceId);
            Environment.SetEnvironmentVariable("Security:Http:ClientCertificate:Enabled", "true");
            Environment.SetEnvironmentVariable("Security:Http:ClientCertificate:FindType", "FindByThumbprint");
            Environment.SetEnvironmentVariable("Security:Http:ClientCertificate:FindValue", cert.Thumbprint);
            var baseUrl = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).AddEnvironmentVariables().Build()["Server:BaseUrl"] ?? "https://localhost:5001";
            using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri(baseUrl) };
            var payload = new { deviceId, thumbprint = cert.Thumbprint, commonName = cert.Subject, certificateDerBase64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert)) };
            var resp = http.PostAsJsonAsync("api/devices/enroll", payload).GetAwaiter().GetResult();
            resp.EnsureSuccessStatusCode();
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
                if (c.Subject.Contains(deviceId, StringComparison.OrdinalIgnoreCase) && DateTime.UtcNow >= c.NotBefore.ToUniversalTime() && DateTime.UtcNow <= c.NotAfter.ToUniversalTime())
                    return c;
            }
        }
        catch { }
        return null;
    }

    private static X509Certificate2 CreateSelfSigned(string deviceId)
    {
        var name = new X500DistinguishedName($"CN=GGs Device {deviceId}");
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(3));
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(cert);
        return cert;
    }
}
