using System.Security.Cryptography.X509Certificates;
using GGs.Server.Data;
using GGs.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Services;

public interface IDeviceIdentityService
{
    Task<DeviceRegistration?> ValidateDeviceCertificateAsync(X509Certificate2 certificate);
    Task<DeviceRegistration> RegisterDeviceAsync(string deviceId, string thumbprint, string? commonName = null);
    Task<bool> RevokeDeviceAsync(string deviceId);
    Task<IList<DeviceRegistration>> GetRegisteredDevicesAsync();
}

public class DeviceIdentityService : IDeviceIdentityService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeviceIdentityService> _logger;

    public DeviceIdentityService(AppDbContext db, ILogger<DeviceIdentityService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DeviceRegistration?> ValidateDeviceCertificateAsync(X509Certificate2 certificate)
    {
        var thumbprint = certificate.GetCertHashString();
        var device = await _db.DeviceRegistrations.FirstOrDefaultAsync(d => d.Thumbprint == thumbprint && d.IsActive);

        if (device == null)
        {
            _logger.LogWarning("Certificate validation failed for thumbprint: {Thumbprint}", thumbprint);
            return null;
        }

        // Update last seen
        device.LastSeenUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Device validated: {DeviceId}", device.DeviceId);
        return device;
    }

    public async Task<DeviceRegistration> RegisterDeviceAsync(string deviceId, string thumbprint, string? commonName = null)
    {
        var existing = await _db.DeviceRegistrations.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (existing == null)
        {
            existing = new DeviceRegistration
            {
                DeviceId = deviceId,
                Thumbprint = thumbprint,
                CommonName = commonName,
                RegisteredUtc = DateTime.UtcNow,
                LastSeenUtc = DateTime.UtcNow,
                IsActive = true
            };
            _db.DeviceRegistrations.Add(existing);
        }
        else
        {
            existing.Thumbprint = thumbprint;
            existing.CommonName = commonName;
            existing.IsActive = true;
            existing.LastSeenUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Device registered: {DeviceId}", deviceId);
        return existing;
    }

    public async Task<bool> RevokeDeviceAsync(string deviceId)
    {
        var device = await _db.DeviceRegistrations.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (device == null) return false;

        device.IsActive = false;
        device.RevokedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogWarning("Device revoked: {DeviceId}", deviceId);
        return true;
    }

    public async Task<IList<DeviceRegistration>> GetRegisteredDevicesAsync()
    {
        return await _db.DeviceRegistrations.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderByDescending(d => d.LastSeenUtc)
            .ToListAsync();
    }
}
