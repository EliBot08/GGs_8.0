using Xunit;
using GGs.Shared.Privacy;
using GGs.Shared.Tweaks;
using System;
using System.Linq;

namespace GGs.Enterprise.Tests;

/// <summary>
/// Tests for Prompt 4: Telemetry, Correlation, and Trace Depth
/// </summary>
public class Prompt4TelemetryTests
{
    [Fact]
    public void TelemetryContext_Create_ShouldGenerateUniqueIds()
    {
        // Arrange
        var deviceId = "test-device-123";
        var correlationId = "test-correlation-456";
        var userId = "test-user-789";

        // Act
        var context = TelemetryContext.Create(deviceId, correlationId, userId);

        // Assert
        Assert.Equal(deviceId, context.DeviceId);
        Assert.Equal(correlationId, context.CorrelationId);
        Assert.Equal(userId, context.UserId);
        Assert.NotNull(context.OperationId);
        Assert.NotEmpty(context.OperationId);
        Assert.True(context.InitiatedUtc <= DateTime.UtcNow);
        Assert.True(context.InitiatedUtc >= DateTime.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public void TelemetryContext_CreateChild_ShouldInheritCorrelation()
    {
        // Arrange
        var parent = TelemetryContext.Create("device-1", "correlation-1", "user-1");

        // Act
        var child = parent.CreateChild();

        // Assert
        Assert.Equal(parent.DeviceId, child.DeviceId);
        Assert.Equal(parent.CorrelationId, child.CorrelationId);
        Assert.Equal(parent.UserId, child.UserId);
        Assert.NotEqual(parent.OperationId, child.OperationId);
        Assert.Equal(parent.OperationId, child.ParentOperationId);
    }

    [Fact]
    public void TelemetryContext_ToDictionary_ShouldContainAllFields()
    {
        // Arrange
        var context = TelemetryContext.Create("device-1", "correlation-1", "user-1");

        // Act
        var dict = context.ToDictionary();

        // Assert
        Assert.Contains("DeviceId", dict.Keys);
        Assert.Contains("OperationId", dict.Keys);
        Assert.Contains("CorrelationId", dict.Keys);
        Assert.Contains("InitiatedUtc", dict.Keys);
        Assert.Contains("UserId", dict.Keys);
        Assert.Equal("device-1", dict["DeviceId"]);
        Assert.Equal("correlation-1", dict["CorrelationId"]);
        Assert.Equal("user-1", dict["UserId"]);
    }

    [Fact]
    public void ReasonCodes_PolicyDenyServiceStop_ShouldFormatCorrectly()
    {
        // Act
        var reasonCode = ReasonCodes.PolicyDenyServiceStop("WinDefend");

        // Assert
        Assert.Equal("POLICY.DENY.ServiceStop.WinDefend", reasonCode);
    }

    [Fact]
    public void ReasonCodes_ExecutionFailedPermissionDenied_ShouldFormatCorrectly()
    {
        // Act
        var reasonCode = ReasonCodes.ExecutionFailedPermissionDenied("Registry");

        // Assert
        Assert.Equal("EXECUTION.FAILED.PermissionDenied.Registry", reasonCode);
    }

    [Fact]
    public void ReasonCodes_NetworkFailedStatusCode_ShouldFormatCorrectly()
    {
        // Act
        var reasonCode = ReasonCodes.NetworkFailedStatusCode(404);

        // Assert
        Assert.Equal("NETWORK.FAILED.StatusCode.404", reasonCode);
    }

    [Fact]
    public void ReasonCodes_Parse_ShouldExtractComponents()
    {
        // Arrange
        var reasonCode = "POLICY.DENY.ServiceStop.WinDefend";

        // Act
        var (category, action, context, detail) = ReasonCodes.Parse(reasonCode);

        // Assert
        Assert.Equal("POLICY", category);
        Assert.Equal("DENY", action);
        Assert.Equal("ServiceStop", context);
        Assert.Equal("WinDefend", detail);
    }

    [Fact]
    public void ReasonCodes_IsSuccess_ShouldIdentifySuccessCodes()
    {
        // Arrange
        var successCodes = new[]
        {
            ReasonCodes.EXECUTION_SUCCESS,
            ReasonCodes.POLICY_ALLOW,
            ReasonCodes.ELEVATION_GRANTED,
            ReasonCodes.PREFLIGHT_PASSED
        };

        var failureCodes = new[]
        {
            ReasonCodes.EXECUTION_FAILED,
            ReasonCodes.POLICY_DENY,
            ReasonCodes.ELEVATION_DENIED_USER,
            ReasonCodes.PREFLIGHT_FAILED
        };

        // Act & Assert
        foreach (var code in successCodes)
        {
            Assert.True(ReasonCodes.IsSuccess(code), $"Expected {code} to be success");
        }

        foreach (var code in failureCodes)
        {
            Assert.False(ReasonCodes.IsSuccess(code), $"Expected {code} to not be success");
        }
    }

    [Fact]
    public void ReasonCodes_IsPolicyDenial_ShouldIdentifyPolicyDenials()
    {
        // Arrange
        var policyDenials = new[]
        {
            ReasonCodes.POLICY_DENY,
            ReasonCodes.PolicyDenyServiceStop("WinDefend"),
            ReasonCodes.PolicyDenyRegistryPath("HKLM\\System")
        };

        var nonPolicyDenials = new[]
        {
            ReasonCodes.POLICY_ALLOW,
            ReasonCodes.EXECUTION_FAILED,
            ReasonCodes.VALIDATION_FAILED
        };

        // Act & Assert
        foreach (var code in policyDenials)
        {
            Assert.True(ReasonCodes.IsPolicyDenial(code), $"Expected {code} to be policy denial");
        }

        foreach (var code in nonPolicyDenials)
        {
            Assert.False(ReasonCodes.IsPolicyDenial(code), $"Expected {code} to not be policy denial");
        }
    }

    [Fact]
    public void ReasonCodes_IsValidationFailure_ShouldIdentifyValidationFailures()
    {
        // Arrange
        var validationFailures = new[]
        {
            ReasonCodes.VALIDATION_FAILED,
            ReasonCodes.ValidationFailedMissingField("TweakId"),
            ReasonCodes.ValidationFailedInvalidFormat("Email")
        };

        var nonValidationFailures = new[]
        {
            ReasonCodes.EXECUTION_FAILED,
            ReasonCodes.POLICY_DENY,
            ReasonCodes.NETWORK_FAILED
        };

        // Act & Assert
        foreach (var code in validationFailures)
        {
            Assert.True(ReasonCodes.IsValidationFailure(code), $"Expected {code} to be validation failure");
        }

        foreach (var code in nonValidationFailures)
        {
            Assert.False(ReasonCodes.IsValidationFailure(code), $"Expected {code} to not be validation failure");
        }
    }

    [Fact]
    public void TweakApplicationLog_ShouldHaveTelemetryFields()
    {
        // Arrange & Act
        var log = new TweakApplicationLog
        {
            TweakId = Guid.NewGuid(),
            DeviceId = "device-1",
            OperationId = "operation-1",
            CorrelationId = "correlation-1",
            InitiatedUtc = DateTime.UtcNow.AddSeconds(-5),
            AppliedUtc = DateTime.UtcNow.AddSeconds(-3),
            CompletedUtc = DateTime.UtcNow,
            Success = true,
            ExecutionTimeMs = 2000,
            ReasonCode = ReasonCodes.EXECUTION_SUCCESS,
            PolicyDecision = "Execution completed successfully"
        };

        // Assert
        Assert.NotEqual(Guid.Empty, log.TweakId);
        Assert.Equal("device-1", log.DeviceId);
        Assert.Equal("operation-1", log.OperationId);
        Assert.Equal("correlation-1", log.CorrelationId);
        Assert.True(log.InitiatedUtc < log.AppliedUtc);
        Assert.True(log.AppliedUtc < log.CompletedUtc);
        Assert.Equal(ReasonCodes.EXECUTION_SUCCESS, log.ReasonCode);
        Assert.NotNull(log.PolicyDecision);
    }

    [Fact]
    public void EnhancedHeartbeatData_ShouldContainAllRequiredFields()
    {
        // Arrange
        var context = TelemetryContext.Create("device-1", "correlation-1");

        // Act - with privacy sanitization
        var machineNameHash = PrivacySanitizer.SanitizeMachineName("TEST-MACHINE");
        var heartbeat = new EnhancedHeartbeatData
        {
            Context = context,
            Timestamp = DateTime.UtcNow,
            AgentVersion = "1.0.0",
            OSVersion = "Windows 11",
            MachineNameHash = machineNameHash,
            ProcessorCount = 8,
            SystemUptime = TimeSpan.FromHours(24),
            ProcessUptime = TimeSpan.FromMinutes(30),
            ConnectionState = "Connected",
            WorkingSetBytes = 100_000_000,
            OperationsExecuted = 100,
            OperationsSucceeded = 95,
            OperationsFailed = 5,
            HealthScore = 85
        };

        // Assert
        Assert.Equal(context, heartbeat.Context);
        Assert.Equal("1.0.0", heartbeat.AgentVersion);
        Assert.Equal("Windows 11", heartbeat.OSVersion);
        Assert.Equal(machineNameHash, heartbeat.MachineNameHash);
        Assert.Equal(8, heartbeat.ProcessorCount);
        Assert.Equal(TimeSpan.FromHours(24), heartbeat.SystemUptime);
        Assert.Equal(TimeSpan.FromMinutes(30), heartbeat.ProcessUptime);
        Assert.Equal("Connected", heartbeat.ConnectionState);
        Assert.Equal(100_000_000, heartbeat.WorkingSetBytes);
        Assert.Equal(100, heartbeat.OperationsExecuted);
        Assert.Equal(95, heartbeat.OperationsSucceeded);
        Assert.Equal(5, heartbeat.OperationsFailed);
        Assert.Equal(85, heartbeat.HealthScore);
    }
}

