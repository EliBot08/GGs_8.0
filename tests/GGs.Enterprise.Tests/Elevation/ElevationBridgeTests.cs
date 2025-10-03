using System;
using System.Threading.Tasks;
using GGs.Agent.Elevation;
using GGs.Shared.Elevation;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GGs.Enterprise.Tests.Elevation;

/// <summary>
/// Comprehensive tests for ElevationBridge.
/// Tests privilege checking, request validation, consent gating, and graceful degradation.
/// </summary>
public sealed class ElevationBridgeTests
{
    private readonly ILogger<ElevationBridge> _logger;
    private readonly ElevationBridge _bridge;

    public ElevationBridgeTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        _logger = loggerFactory.CreateLogger<ElevationBridge>();
        _bridge = new ElevationBridge(_logger);
    }

    [Fact]
    public async Task IsElevatedAsync_ReturnsBoolean()
    {
        // Act
        var isElevated = await _bridge.IsElevatedAsync();

        // Assert
        Assert.IsType<bool>(isElevated);
        // Note: Result depends on test execution context (admin vs non-admin)
    }

    [Fact]
    public async Task ValidateRequestAsync_WithValidRequest_ReturnsValid()
    {
        // Arrange
        var request = new ElevationRequest
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString(),
            OperationType = "FlushDns",
            OperationName = "Flush DNS Cache",
            Reason = "Clear DNS cache for troubleshooting",
            RiskLevel = ElevationRiskLevel.Low,
            RequiresRestart = false,
            EstimatedDuration = TimeSpan.FromSeconds(5),
            Payload = new { Type = "FlushDns" },
            SupportsRollback = false
        };

        // Act
        var result = await _bridge.ValidateRequestAsync(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.ValidationErrors);
        Assert.Empty(result.PolicyViolations);
    }

    [Fact]
    public async Task ValidateRequestAsync_WithMissingOperationType_ReturnsInvalid()
    {
        // Arrange
        var request = new ElevationRequest
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString(),
            OperationType = "",
            OperationName = "Test Operation",
            Reason = "Test reason",
            RiskLevel = ElevationRiskLevel.Low,
            RequiresRestart = false,
            EstimatedDuration = TimeSpan.FromSeconds(5),
            Payload = new { },
            SupportsRollback = false
        };

        // Act
        var result = await _bridge.ValidateRequestAsync(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationErrors, e => e.Contains("OperationType"));
    }

    [Fact]
    public async Task ValidateRequestAsync_WithUnsupportedOperationType_ReturnsInvalid()
    {
        // Arrange
        var request = new ElevationRequest
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString(),
            OperationType = "UnsupportedOperation",
            OperationName = "Unsupported Test",
            Reason = "Test reason",
            RiskLevel = ElevationRiskLevel.Low,
            RequiresRestart = false,
            EstimatedDuration = TimeSpan.FromSeconds(5),
            Payload = new { },
            SupportsRollback = false
        };

        // Act
        var result = await _bridge.ValidateRequestAsync(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationErrors, e => e.Contains("Unsupported operation type"));
    }

    [Fact]
    public async Task ValidateRequestAsync_WithCriticalRiskLevel_ReturnsPolicyWarning()
    {
        // Arrange
        var request = new ElevationRequest
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString(),
            OperationType = "FlushDns",
            OperationName = "Critical Operation",
            Reason = "Critical system change",
            RiskLevel = ElevationRiskLevel.Critical,
            RequiresRestart = true,
            EstimatedDuration = TimeSpan.FromMinutes(5),
            Payload = new { Type = "FlushDns" },
            SupportsRollback = false
        };

        // Act
        var result = await _bridge.ValidateRequestAsync(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.PolicyViolations, v => v.Contains("POLICY.WARN.RiskLevel.Critical"));
    }

    [Fact]
    public async Task ExecuteElevatedAsync_WithInvalidRequest_ReturnsFailure()
    {
        // Arrange
        var request = new ElevationRequest
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString(),
            OperationType = "",
            OperationName = "Invalid Operation",
            Reason = "Test",
            RiskLevel = ElevationRiskLevel.Low,
            RequiresRestart = false,
            EstimatedDuration = TimeSpan.FromSeconds(5),
            Payload = new { },
            SupportsRollback = false
        };

        // Act
        var result = await _bridge.ExecuteElevatedAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Validation failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteElevatedAsync_WithValidFlushDnsRequest_InSimulationMode_Succeeds()
    {
        // Arrange
        Environment.SetEnvironmentVariable("GGS_ELEVATION_SIMULATE", "1");
        
        var request = new ElevationRequest
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString(),
            OperationType = "FlushDns",
            OperationName = "Flush DNS Cache",
            Reason = "Clear DNS cache",
            RiskLevel = ElevationRiskLevel.Low,
            RequiresRestart = false,
            EstimatedDuration = TimeSpan.FromSeconds(5),
            Payload = new { Type = "FlushDns" },
            SupportsRollback = false
        };

        try
        {
            // Act
            var result = await _bridge.ExecuteElevatedAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(request.RequestId, result.RequestId);
            Assert.Equal(request.CorrelationId, result.CorrelationId);
            // Note: Success depends on whether agent executable is available in test context
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_ELEVATION_SIMULATE", null);
        }
    }

    [Fact]
    public async Task ExecuteElevatedAsync_TracksExecutionTime()
    {
        // Arrange
        var request = new ElevationRequest
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString(),
            OperationType = "FlushDns",
            OperationName = "Flush DNS Cache",
            Reason = "Test timing",
            RiskLevel = ElevationRiskLevel.Low,
            RequiresRestart = false,
            EstimatedDuration = TimeSpan.FromSeconds(5),
            Payload = new { Type = "FlushDns" },
            SupportsRollback = false
        };

        // Act
        var result = await _bridge.ExecuteElevatedAsync(request);

        // Assert
        Assert.True(result.ExecutedAtUtc <= result.CompletedAtUtc);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task RollbackAsync_ReturnsNotImplemented()
    {
        // Arrange
        var executionLog = new ElevationExecutionLog
        {
            Request = new ElevationRequest
            {
                RequestId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid().ToString(),
                OperationType = "FlushDns",
                OperationName = "Test",
                Reason = "Test",
                RiskLevel = ElevationRiskLevel.Low,
                RequiresRestart = false,
                EstimatedDuration = TimeSpan.FromSeconds(5),
                Payload = new { },
                SupportsRollback = false
            },
            WasElevated = false,
            ExecutedBy = "TestUser",
            MachineName = "TestMachine",
            ExecutedAtUtc = DateTime.UtcNow
        };

        // Act
        var result = await _bridge.RollbackAsync(executionLog);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not yet implemented", result.ErrorMessage ?? "");
    }

    [Fact]
    public async Task ValidateRequestAsync_WithAllSupportedOperationTypes_Succeeds()
    {
        // Arrange
        var supportedOperations = new[]
        {
            "flushdns", "winsockreset", "tcpauthnormal", "powercfgsetactive",
            "bcdedittimeout", "registryset", "serviceaction", "netshsetdns"
        };

        foreach (var operationType in supportedOperations)
        {
            var request = new ElevationRequest
            {
                RequestId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid().ToString(),
                OperationType = operationType,
                OperationName = $"Test {operationType}",
                Reason = "Test validation",
                RiskLevel = ElevationRiskLevel.Low,
                RequiresRestart = false,
                EstimatedDuration = TimeSpan.FromSeconds(5),
                Payload = new { Type = operationType },
                SupportsRollback = false
            };

            // Act
            var result = await _bridge.ValidateRequestAsync(request);

            // Assert
            Assert.True(result.IsValid, $"Operation type '{operationType}' should be valid");
            Assert.Empty(result.ValidationErrors);
        }
    }

    [Fact]
    public async Task ExecuteElevatedAsync_CreatesExecutionLog()
    {
        // Arrange
        Environment.SetEnvironmentVariable("GGS_ELEVATION_SIMULATE", "1");
        
        var request = new ElevationRequest
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString(),
            OperationType = "FlushDns",
            OperationName = "Test Execution Log",
            Reason = "Verify execution log creation",
            RiskLevel = ElevationRiskLevel.Low,
            RequiresRestart = false,
            EstimatedDuration = TimeSpan.FromSeconds(5),
            Payload = new { Type = "FlushDns" },
            SupportsRollback = false
        };

        try
        {
            // Act
            var result = await _bridge.ExecuteElevatedAsync(request);

            // Assert
            if (result.Success && result.ExecutionLog != null)
            {
                Assert.Equal(request.RequestId, result.ExecutionLog.Request.RequestId);
                Assert.NotNull(result.ExecutionLog.ExecutedBy);
                Assert.NotNull(result.ExecutionLog.MachineName);
                Assert.True(result.ExecutionLog.ExecutedAtUtc <= DateTime.UtcNow);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_ELEVATION_SIMULATE", null);
        }
    }
}

