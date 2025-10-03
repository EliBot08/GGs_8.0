# ADR-005: Telemetry, Correlation, and Trace Depth

**Status**: ✅ Implemented  
**Date**: 2025-10-03  
**Prompt**: Prompt 4 — Telemetry, Correlation, and Trace Depth

---

## Context and Problem Statement

The GGs.Agent system requires comprehensive telemetry, correlation, and tracing capabilities to support:
- Distributed tracing across multiple systems and operations
- Long-term analytics and compliance reporting
- Real-time monitoring and alerting
- Root cause analysis for failures
- Performance optimization

**Requirements from EliNextSteps:**
- Every operation carries DeviceId + OperationId + CorrelationId with synchronized timestamps
- Persist to: hub ACK, HTTP audit endpoint, and encrypted offline queue
- Heartbeats include agent version, OS, uptime, working set, CPU count, connection state
- Detailed health opts in via `Agent:SendDetailedHealthData=true`
- Normalize logs with reason codes (e.g., `POLICY.DENY.ServiceStop.WinDefend`)
- Stable schemas for long-term analytics

---

## Decision

We implement a comprehensive telemetry system with the following components:

### 1. Telemetry Context (`TelemetryContext`)

A structured context object that flows through all operations:

```csharp
public sealed class TelemetryContext
{
    public required string DeviceId { get; init; }        // Stable device identifier
    public required string OperationId { get; init; }     // Unique per operation
    public required string CorrelationId { get; init; }   // Links related operations
    public DateTime InitiatedUtc { get; init; }           // Synchronized timestamp
    public string? UserId { get; init; }                  // Optional user context
    public string? SessionId { get; init; }               // Optional session context
    public string? ParentOperationId { get; init; }       // For operation hierarchies
}
```

**Key Features:**
- Immutable by design (init-only properties)
- Factory methods for creation and child context generation
- Conversion to dictionary for logging and tracing
- Formatted string representation for structured logging

### 2. Enhanced TweakApplicationLog

Extended with telemetry fields:

```csharp
public sealed class TweakApplicationLog
{
    // Existing fields...
    
    // Prompt 4: Enhanced Telemetry
    public string OperationId { get; set; }           // Unique operation ID
    public string? CorrelationId { get; set; }        // Correlation ID
    public DateTime InitiatedUtc { get; set; }        // Operation start time
    public DateTime? CompletedUtc { get; set; }       // Operation end time
    public string? ReasonCode { get; set; }           // Standardized reason code
    public string? PolicyDecision { get; set; }       // Policy decision context
}
```

**Database Migration:**
- Added columns: `OperationId`, `CorrelationId`, `InitiatedUtc`, `CompletedUtc`, `ReasonCode`, `PolicyDecision`
- Indexes on: `OperationId`, `CorrelationId`, `InitiatedUtc`, `ReasonCode` for efficient querying

### 3. Standardized Reason Codes (`ReasonCodes`)

A comprehensive set of machine-readable reason codes following the format:
**`CATEGORY.ACTION.Context.Detail`**

**Categories:**
- `POLICY.*` - Policy enforcement decisions
- `VALIDATION.*` - Input validation failures
- `EXECUTION.*` - Operation execution results
- `ELEVATION.*` - Privilege elevation results
- `CONSENT.*` - User consent decisions
- `PREFLIGHT.*` - Pre-execution checks
- `ROLLBACK.*` - Rollback operations
- `AUDIT.*` - Audit logging results
- `NETWORK.*` - Network operation results
- `QUEUE.*` - Offline queue operations

**Examples:**
```csharp
ReasonCodes.PolicyDenyServiceStop("WinDefend")
// → "POLICY.DENY.ServiceStop.WinDefend"

ReasonCodes.ExecutionFailedPermissionDenied("Registry")
// → "EXECUTION.FAILED.PermissionDenied.Registry"

ReasonCodes.NetworkFailedStatusCode(404)
// → "NETWORK.FAILED.StatusCode.404"
```

**Helper Methods:**
- `Parse(reasonCode)` - Parses code into components
- `IsSuccess(reasonCode)` - Checks if code indicates success
- `IsPolicyDenial(reasonCode)` - Checks for policy denials
- `IsValidationFailure(reasonCode)` - Checks for validation failures

### 4. Enhanced Heartbeat Data (`EnhancedHeartbeatData`)

Comprehensive system health monitoring:

**Basic Metrics (Always Sent):**
- DeviceId, OperationId, Timestamp
- AgentVersion, OSVersion, MachineName
- ProcessorCount, SystemUptime, ProcessUptime
- ConnectionState

**Detailed Metrics (Opt-in via `Agent:SendDetailedHealthData=true`):**
- WorkingSetBytes, PrivateMemoryBytes, VirtualMemoryBytes
- CpuUsagePercent, TotalPhysicalMemoryBytes, AvailablePhysicalMemoryBytes
- ThreadCount, HandleCount
- GCTotalMemoryBytes, GCGen0/1/2Collections
- OperationsExecuted/Succeeded/Failed (since last heartbeat)
- OfflineQueueDepth
- HealthScore (0-100)
- CustomMetrics (extensible)

**Health Score Calculation:**
```csharp
int score = 100;
- Deduct for high memory usage (>500MB working set)
- Deduct for high thread count (>50 threads)
- Deduct for high handle count (>1000 handles)
- Deduct for excessive GC Gen2 collections (>100)
= Final score (0-100)
```

### 5. Enhanced Worker Implementation

**Telemetry Integration:**
- ActivitySource for distributed tracing
- Operation counters for heartbeat reporting
- Telemetry context flows through all operations
- Reason codes attached to all log entries
- Enhanced HTTP headers: `X-Correlation-ID`, `X-Operation-ID`, `X-Device-ID`

**Audit Logging:**
- Primary endpoint: `/api/audit/log` (secure)
- Fallback endpoint: `/api/auditlogs` (legacy)
- Offline queue for failed audits
- Reason codes for all outcomes

**Heartbeat:**
- Basic heartbeat always sent
- Enhanced heartbeat opt-in via configuration
- Operation counters reset after each heartbeat
- Health score calculated and reported

---

## Consequences

### Positive

✅ **Comprehensive Observability**
- Full distributed tracing with OperationId and CorrelationId
- Synchronized timestamps for accurate time-series analysis
- Standardized reason codes for machine-readable analytics

✅ **Long-Term Analytics**
- Stable schemas enable historical analysis
- Reason codes allow aggregation and filtering
- Database indexes optimize query performance

✅ **Real-Time Monitoring**
- Enhanced heartbeats provide detailed health metrics
- Operation counters track throughput and success rates
- Health scores enable proactive alerting

✅ **Compliance and Audit**
- Complete audit trail with telemetry context
- Policy decisions captured for compliance reporting
- Offline queue ensures no data loss

✅ **Troubleshooting**
- Correlation IDs link related operations
- Reason codes provide root cause information
- Detailed error context aids debugging

### Neutral

⚠️ **Configuration Complexity**
- Opt-in detailed health monitoring requires configuration
- Reason code standardization requires discipline

⚠️ **Storage Requirements**
- Additional telemetry fields increase database size
- Offline queue requires disk space

### Negative

❌ **Performance Overhead**
- Telemetry collection adds minimal CPU/memory overhead
- Mitigation: Detailed health is opt-in, basic telemetry is lightweight

❌ **Network Bandwidth**
- Enhanced heartbeats increase network traffic
- Mitigation: Opt-in configuration, configurable intervals

---

## Implementation Details

### Configuration

**appsettings.json:**
```json
{
  "Agent": {
    "HeartbeatIntervalSeconds": 30,
    "SendDetailedHealthData": false
  }
}
```

**Environment Variables:**
- `AGENT_MACHINE_TOKEN` - Machine authentication token
- `GGS_DATA_DIR` - Override data directory for offline queue

### Database Schema

**TweakLogs Table:**
```sql
CREATE TABLE TweakLogs (
    Id TEXT PRIMARY KEY,
    TweakId TEXT NOT NULL,
    DeviceId TEXT NOT NULL,
    OperationId TEXT NOT NULL,
    CorrelationId TEXT,
    InitiatedUtc TEXT NOT NULL,
    AppliedUtc TEXT NOT NULL,
    CompletedUtc TEXT,
    ExecutionTimeMs INTEGER NOT NULL,
    Success INTEGER NOT NULL,
    Error TEXT,
    ReasonCode TEXT,
    PolicyDecision TEXT,
    -- ... other fields
    INDEX IX_TweakLogs_OperationId (OperationId),
    INDEX IX_TweakLogs_CorrelationId (CorrelationId),
    INDEX IX_TweakLogs_InitiatedUtc (InitiatedUtc),
    INDEX IX_TweakLogs_ReasonCode (ReasonCode)
);
```

### HTTP Headers

All audit log requests include:
- `X-Correlation-ID` - Correlation identifier
- `X-Operation-ID` - Operation identifier
- `X-Device-ID` - Device identifier
- `X-Machine-Token` - Authentication token (if configured)
- `User-Agent` - "GGs.Agent/1.0"

---

## Testing

### Unit Tests Required

- [ ] TelemetryContext creation and child context generation
- [ ] ReasonCodes parsing and helper methods
- [ ] EnhancedHeartbeatData serialization
- [ ] Health score calculation
- [ ] Reason code assignment in Worker

### Integration Tests Required

- [ ] End-to-end telemetry flow from ExecuteTweak to audit log
- [ ] Correlation ID propagation across operations
- [ ] Enhanced heartbeat with detailed health data
- [ ] Offline queue storage and retrieval
- [ ] Reason code filtering and aggregation

---

## References

- EliNextSteps: Prompt 4 — Telemetry, Correlation, and Trace Depth
- OpenTelemetry specification for distributed tracing
- W3C Trace Context specification
- Structured logging best practices

---

## Changelog

- **2025-10-03**: Initial implementation
  - Added TelemetryContext model
  - Added ReasonCodes helper class
  - Added EnhancedHeartbeatData model
  - Enhanced TweakApplicationLog with telemetry fields
  - Updated Worker with telemetry integration
  - Created database migration
  - Added comprehensive documentation

