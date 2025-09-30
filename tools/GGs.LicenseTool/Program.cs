using System;
using System.Text.Json;
using GGs.Shared.Enums;
using GGs.Shared.Licensing;
using GGs.Shared.Platform;

var deviceId = DeviceIdHelper.GetStableDeviceId();
var (priv, pub) = RsaLicenseService.CreateRsaKeyPair();

var payload = new LicensePayload
{
    LicenseId = Guid.NewGuid().ToString(),
    UserId = "admin@ggs.local",
    Tier = LicenseTier.Admin,
    IssuedUtc = DateTime.UtcNow,
    ExpiresUtc = null,
    IsAdminKey = true,
    DeviceBindingId = deviceId,
    AllowOfflineValidation = true,
    Notes = "Permanent Admin license (device-bound)"
};

var signed = RsaLicenseService.Sign(payload, priv);
var result = new
{
    license = signed,
    publicKeyPem = pub,
    fingerprint = RsaLicenseService.ComputePublicKeyFingerprint(pub)
};
var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
});
Console.WriteLine(json);
