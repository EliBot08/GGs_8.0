using Microsoft.Extensions.Options;

namespace GGs.Server.Services;

public interface ILicenseKeyProvider
{
    string? GetPrivateKeyPem();
    string? GetPublicKeyPem();
}

public sealed class OptionsLicenseKeyProvider : ILicenseKeyProvider
{
    private readonly IOptionsMonitor<LicenseKeysOptions> _opts;
    public OptionsLicenseKeyProvider(IOptionsMonitor<LicenseKeysOptions> opts) { _opts = opts; }
    public string? GetPrivateKeyPem() => _opts.CurrentValue.PrivateKeyPem;
    public string? GetPublicKeyPem() => _opts.CurrentValue.PublicKeyPem;
}


