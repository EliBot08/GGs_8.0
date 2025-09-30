using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GGs.Shared.CloudProfiles;

public sealed class CloudProfilePayload
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Publisher { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public string ContentHash { get; set; } = string.Empty; // SHA256 hex
    public string Category { get; set; } = "general";
    public bool ModerationApproved { get; set; }
    public string? ModerationNote { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public string Content { get; set; } = string.Empty; // JSON/YAML profile content
}

public sealed class SignedCloudProfile
{
    public CloudProfilePayload Payload { get; set; } = new();
    public string Signature { get; set; } = string.Empty; // base64
    public string KeyFingerprint { get; set; } = string.Empty; // hex

    public SignedCloudProfile() { }

    public SignedCloudProfile(CloudProfilePayload payload, string signature, string keyFingerprint)
    {
        Payload = payload;
        Signature = signature;
        KeyFingerprint = keyFingerprint;
    }
}

public sealed class CloudProfileSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool ModerationApproved { get; set; }
}

public sealed class CloudProfileSearchRequest
{
    public string? Query { get; set; }
    public string? Category { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class CloudProfilePage
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public List<CloudProfileSummary> Items { get; set; } = new();
}

