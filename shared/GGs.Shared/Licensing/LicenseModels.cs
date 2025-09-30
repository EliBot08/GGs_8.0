using System.Text.Json.Serialization;
using GGs.Shared.Enums;

namespace GGs.Shared.Licensing;

public sealed class LicensePayload
{
    public required string LicenseId { get; init; } // GUID string
    public required string UserId { get; init; } // server-side identity id
    public required LicenseTier Tier { get; init; }
    public DateTime IssuedUtc { get; init; }
    public DateTime? ExpiresUtc { get; init; }
    public bool IsAdminKey { get; init; }
    public string? DeviceBindingId { get; init; } // optional device binding (hashed)
    public bool AllowOfflineValidation { get; init; } = true;
    public string? Notes { get; init; }

    // Extra claims/flags for future-proofing
    public Dictionary<string, string>? Claims { get; init; }
}

public sealed class SignedLicense
{
    public required LicensePayload Payload { get; init; }

    // Base64-encoded detached signature over canonical JSON payload
    public required string Signature { get; init; }

    // Public key fingerprint to support key rotation (SHA-256 over DER public key)
    public required string KeyFingerprint { get; init; }
}
