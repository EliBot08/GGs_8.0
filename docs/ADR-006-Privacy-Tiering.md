# ADR-006: Privacy Tiering and Data Classification

**Status:** ✅ Implemented  
**Date:** 2025-10-03  
**Context:** Prompt 5 — Safety, Policy, and Compliance

## Decision

Implement a comprehensive privacy tiering system that classifies all telemetry and system data by sensitivity level, applies appropriate redaction or hashing, and provides explicit configuration flags to control data collection scope.

## Privacy Tiers

### Tier 0: Public
- **Risk Level:** None
- **Examples:** OS version, processor count, agent version
- **Treatment:** Collected and transmitted as-is
- **Rationale:** No privacy concerns; useful for analytics and support

### Tier 1: Internal
- **Risk Level:** Low
- **Examples:** Performance metrics, health scores, CPU usage, memory usage
- **Treatment:** Collected and transmitted as-is
- **Rationale:** Aggregated metrics with no personally identifiable information

### Tier 2: Confidential
- **Risk Level:** Medium
- **Examples:** Machine names, IP addresses, network adapter details
- **Treatment:** Hashed by default (SHA256, first 16 chars)
- **Configuration:** Can be disabled via `GGS_PRIVACY_COLLECT_MACHINE_NAMES=false`
- **Rationale:** Allows correlation across sessions while preserving privacy

### Tier 3: Restricted
- **Risk Level:** High
- **Examples:** User names, file paths, registry values, process names
- **Treatment:** Hashed or fully redacted; disabled by default
- **Configuration:** Requires explicit opt-in via environment variables
- **Rationale:** High privacy risk; only collected when explicitly authorized

### Tier 4: Secret
- **Risk Level:** Critical
- **Examples:** Credentials, tokens, API keys, passwords
- **Treatment:** Never logged or transmitted
- **Rationale:** Must never be exposed under any circumstances

## Implementation

### PrivacyClassification.cs
Located at: `GGs/shared/GGs.Shared/Privacy/PrivacyClassification.cs`

**Key Components:**
1. **PrivacyTier enum** - Defines 5 privacy tiers (Public, Internal, Confidential, Restricted, Secret)
2. **PrivacyConfiguration class** - Configuration flags for data collection scope
3. **PrivacySanitizer static class** - Sanitization methods for each data type
4. **PrivacyAwareTelemetry class** - Privacy-aware telemetry data model

### Configuration Flags

All configuration is controlled via environment variables with safe defaults:

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `GGS_PRIVACY_COLLECT_MACHINE_NAMES` | `true` | Collect machine names (hashed) |
| `GGS_PRIVACY_COLLECT_USER_NAMES` | `false` | Collect user names (hashed) |
| `GGS_PRIVACY_COLLECT_IP_ADDRESSES` | `true` | Collect IP addresses (hashed) |
| `GGS_PRIVACY_COLLECT_FILE_PATHS` | `false` | Collect file paths (redacted) |
| `GGS_PRIVACY_COLLECT_REGISTRY_VALUES` | `false` | Collect registry values (redacted) |
| `GGS_PRIVACY_COLLECT_PROCESS_NAMES` | `true` | Collect process names |
| `GGS_PRIVACY_COLLECT_NETWORK_DETAILS` | `true` | Collect network adapter details |
| `GGS_PRIVACY_DETAILED_HEALTH` | `false` | Enable detailed health telemetry |
| `GGS_PRIVACY_HASH_SENSITIVE` | `true` | Hash sensitive fields (vs. redact) |

### Sanitization Methods

```csharp
// Machine name - Confidential tier
PrivacySanitizer.SanitizeMachineName("DESKTOP-ABC123")
// Returns: "A1B2C3D4E5F6G7H8" (SHA256 hash, first 16 chars)

// User name - Restricted tier
PrivacySanitizer.SanitizeUserName("john.doe")
// Returns: "[REDACTED]" (default) or hash if enabled

// File path - Restricted tier
PrivacySanitizer.SanitizeFilePath("C:\\Users\\john.doe\\Documents\\file.txt")
// Returns: "C:\\[USER_PROFILE]\\Documents\\file.txt" or hash

// IP address - Confidential tier
PrivacySanitizer.SanitizeIpAddress("192.168.1.100")
// Returns: "B4C5D6E7F8G9H0I1" (SHA256 hash, first 16 chars)
```

### Hashing Algorithm

- **Algorithm:** SHA256
- **Output:** First 16 characters of hex string
- **Rationale:** 
  - Provides 64 bits of entropy (sufficient for correlation)
  - Deterministic (same input always produces same hash)
  - One-way (cannot reverse to original value)
  - Fast and widely supported

### Integration Points

#### 1. Worker.cs Heartbeat
```csharp
var basicHealthData = new
{
    DeviceId = deviceId,
    MachineNameHash = PrivacySanitizer.SanitizeMachineName(Environment.MachineName),
    // ... other fields
};
```

#### 2. EnhancedHeartbeatData Model
```csharp
public sealed class EnhancedHeartbeatData
{
    public required string MachineNameHash { get; init; } // Changed from MachineName
    // ... other fields
}
```

#### 3. TweakApplicationLog
Future enhancement: Add privacy-aware logging for script content and registry values.

## Benefits

1. **Privacy by Default:** Sensitive data is hashed or redacted by default
2. **Explicit Opt-In:** High-risk data requires explicit configuration
3. **Correlation Capability:** Hashing allows correlation while preserving privacy
4. **Compliance Ready:** Supports GDPR, CCPA, and other privacy regulations
5. **Configurable:** Enterprise customers can adjust privacy settings per their policies
6. **Auditable:** Clear classification and treatment for all data types

## Trade-offs

### Advantages
- Strong privacy protection
- Regulatory compliance
- User trust and transparency
- Flexible configuration

### Disadvantages
- Hashed data cannot be reversed for debugging
- Some diagnostic scenarios may require opt-in to collect detailed data
- Additional complexity in telemetry pipeline

## Alternatives Considered

### 1. No Privacy Controls
**Rejected:** Unacceptable privacy risk; non-compliant with regulations

### 2. Full Redaction (No Hashing)
**Rejected:** Loses correlation capability; harder to track issues across sessions

### 3. Encryption Instead of Hashing
**Rejected:** Requires key management; doesn't prevent access by key holders

### 4. Client-Side Anonymization
**Selected:** Best balance of privacy, utility, and compliance

## Testing

### Unit Tests
- `PrivacySanitizerTests.cs` - Test all sanitization methods
- `PrivacyConfigurationTests.cs` - Test configuration loading
- `Prompt4TelemetryTests.cs` - Updated to use MachineNameHash

### Integration Tests
- Verify heartbeat data contains hashed machine names
- Verify configuration flags control data collection
- Verify Secret-tier data is never logged

### Compliance Tests
- Verify no PII in logs without explicit opt-in
- Verify hashing is deterministic
- Verify redaction is complete

## Monitoring

### Metrics to Track
- Privacy configuration flags in use
- Percentage of hashed vs. redacted fields
- Opt-in rates for detailed telemetry

### Alerts
- Alert if Secret-tier data appears in logs (should never happen)
- Alert if privacy configuration is misconfigured

## Documentation

### User-Facing
- Privacy policy document explaining data collection
- Configuration guide for enterprise administrators
- FAQ on privacy controls

### Developer-Facing
- This ADR
- Code comments in PrivacyClassification.cs
- Integration examples in Worker.cs

## Future Enhancements

1. **Differential Privacy:** Add noise to aggregated metrics
2. **Data Retention Policies:** Automatic deletion of old telemetry
3. **User Consent UI:** In-app privacy controls
4. **Privacy Dashboard:** Show users what data is collected
5. **Audit Logging:** Track all privacy-related configuration changes
6. **Regional Compliance:** Different defaults per region (EU, US, etc.)

## References

- GDPR Article 25: Data Protection by Design and by Default
- CCPA Section 1798.100: Consumer Rights
- NIST Privacy Framework
- ISO/IEC 29100: Privacy Framework
- Microsoft Privacy Guidelines

## Approval

- **Author:** GGs.Agent Team
- **Reviewers:** Security Team, Legal Team, Privacy Officer
- **Status:** ✅ Approved and Implemented
- **Implementation Date:** 2025-10-03

