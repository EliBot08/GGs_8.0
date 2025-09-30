using System.Security.Cryptography.X509Certificates;
using GGs.Server.Services;
using Xunit;

namespace GGs.E2ETests;

public class DeviceIdentityServiceTests
{
    [Fact]
    public async Task Register_Then_Validate_And_Revoke_Works()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceIdentityService>.Instance;
        var svc = new DeviceIdentityService(logger);
        var deviceId = "dev-123";
        var thumb = "ABCDEF012345";

        // Register
        var rec = await svc.RegisterDeviceAsync(deviceId, thumb, "CN=GGs Device");
        Assert.Equal(deviceId, rec.DeviceId);
        Assert.True(rec.IsActive);

        // Validate (simulate cert)
        var cert = new X509Certificate2();
        typeof(X509Certificate2).GetField("m_subjectName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(cert, "CN=GGs Device");
        // We cannot set thumbprint easily; instead validate via service dictionary
        var ok = await svc.ValidateDeviceCertificateAsync(new X509Certificate2Stub(thumb));
        Assert.NotNull(ok);
        Assert.Equal(deviceId, ok!.DeviceId);

        // Revoke
        var revoked = await svc.RevokeDeviceAsync(deviceId);
        Assert.True(revoked);
        var after = await svc.ValidateDeviceCertificateAsync(new X509Certificate2Stub(thumb));
        Assert.Null(after);
    }

    private sealed class X509Certificate2Stub : X509Certificate2
    {
        private readonly string _thumb;
        public X509Certificate2Stub(string thumb) => _thumb = thumb;
        public override string GetCertHashString() => _thumb;
    }
}
