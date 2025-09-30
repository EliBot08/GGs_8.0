using System.Threading.Tasks;
using GGs.Server.Controllers;
using GGs.Server.Services;
using GGs.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GGs.E2ETests;

public class DevicesControllerTests
{
    private sealed class FakeDevices : IDeviceIdentityService
    {
        public List<DeviceRegistration> List = new();
        public Task<DeviceRegistration?> ValidateDeviceCertificateAsync(System.Security.Cryptography.X509Certificates.X509Certificate2 certificate)
            => Task.FromResult<DeviceRegistration?>(null);
        public Task<DeviceRegistration> RegisterDeviceAsync(string deviceId, string thumbprint, string? commonName = null)
        {
            var r = new DeviceRegistration { DeviceId = deviceId, Thumbprint = thumbprint, CommonName = commonName, IsActive = true, RegisteredUtc = DateTime.UtcNow, LastSeenUtc = DateTime.UtcNow };
            List.Add(r);
            return Task.FromResult(r);
        }
        public Task<bool> RevokeDeviceAsync(string deviceId)
        {
            var item = List.FirstOrDefault(x => x.DeviceId == deviceId);
            if (item == null) return Task.FromResult(false);
            item.IsActive = false; item.RevokedUtc = DateTime.UtcNow;
            return Task.FromResult(true);
        }
        public Task<IList<DeviceRegistration>> GetRegisteredDevicesAsync() => Task.FromResult<IList<DeviceRegistration>>(List);
    }

    [Fact]
    public async Task Enroll_Then_List_Then_Revoke()
    {
        var fake = new FakeDevices();
        var ctrl = new DevicesController(fake, NullLogger<DevicesController>.Instance);

        var enroll = await ctrl.Enroll(new DeviceEnrollRequest { DeviceId = "devA", Thumbprint = "THUMB", CommonName = "CN=GGs" });
        var ok = Assert.IsType<OkObjectResult>(enroll);
        var listed = await ctrl.Get();
        var okList = Assert.IsType<OkObjectResult>(listed);
        var list = Assert.IsAssignableFrom<IList<DeviceRegistration>>(okList.Value);
        Assert.Single(list);
        Assert.Equal("devA", list[0].DeviceId);

        var rev = await ctrl.Revoke("devA");
        Assert.IsType<OkResult>(rev);
        Assert.False(list[0].IsActive);
    }
}
