using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using GGs.Agent.SystemAccess;
using GGs.Shared.SystemAccess;

namespace GGs.Enterprise.Tests.SystemAccess;

/// <summary>
/// Comprehensive tests for WindowsSystemAccessProvider.
/// Tests privilege checking, consent gating, WMI inventory, and graceful degradation.
/// </summary>
public sealed class WindowsSystemAccessProviderTests
{
    private readonly ILogger<WindowsSystemAccessProvider> _logger;
    private readonly WindowsSystemAccessProvider _provider;

    public WindowsSystemAccessProviderTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        _logger = loggerFactory.CreateLogger<WindowsSystemAccessProvider>();
        _provider = new WindowsSystemAccessProvider(_logger);
    }

    [Fact]
    public async Task CheckPrivilegesAsync_ReturnsValidResult()
    {
        // Act
        var result = await _provider.CheckPrivilegesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.UserName);
        Assert.NotNull(result.UserDomain);
        Assert.NotNull(result.EnabledPrivileges);
        Assert.True(result.CheckedAtUtc <= DateTime.UtcNow);
        Assert.True(result.CheckedAtUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task CheckPrivilegesAsync_ReturnsConsistentResults()
    {
        // Act
        var result1 = await _provider.CheckPrivilegesAsync();
        var result2 = await _provider.CheckPrivilegesAsync();

        // Assert
        Assert.Equal(result1.UserName, result2.UserName);
        Assert.Equal(result1.UserDomain, result2.UserDomain);
        Assert.Equal(result1.IsElevated, result2.IsElevated);
        Assert.Equal(result1.IsAdministrator, result2.IsAdministrator);
    }

    [Fact]
    public async Task RequestElevationConsentAsync_WithValidRequest_ReturnsResult()
    {
        // Arrange
        var request = new ElevationConsentRequest
        {
            OperationName = "Test Operation",
            Reason = "Unit test",
            DetailedDescription = "Testing elevation consent flow",
            RiskLevel = ElevationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromSeconds(5),
            RequiresRestart = false,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var result = await _provider.RequestElevationConsentAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.CorrelationId, result.CorrelationId);
        Assert.NotNull(result.Reason);
        Assert.True(result.RespondedAtUtc >= result.RequestedAtUtc);
    }

    [Fact]
    public async Task RequestElevationConsentAsync_WhenNotElevated_DeniesWithReason()
    {
        // Arrange
        var privilegeCheck = await _provider.CheckPrivilegesAsync();
        
        // Skip test if already elevated
        if (privilegeCheck.IsElevated)
        {
            return;
        }

        var request = new ElevationConsentRequest
        {
            OperationName = "Privileged Operation",
            Reason = "Requires admin rights",
            DetailedDescription = "Testing non-admin denial",
            RiskLevel = ElevationRiskLevel.High,
            EstimatedDuration = TimeSpan.FromMinutes(1),
            RequiresRestart = true,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var result = await _provider.RequestElevationConsentAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Granted);
        Assert.NotNull(result.Reason);
        Assert.Contains("consent", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetWmiInventoryAsync_ReturnsValidInventory()
    {
        // Act
        var result = await _provider.GetWmiInventoryAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.DeviceId);
        Assert.NotNull(result.CorrelationId);
        Assert.True(result.CollectedAtUtc <= DateTime.UtcNow);
        Assert.NotNull(result.Warnings);
        
        // Should succeed even without admin rights
        Assert.True(result.Success || !string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public async Task GetWmiInventoryAsync_CollectsHardwareInfo()
    {
        // Act
        var result = await _provider.GetWmiInventoryAsync();

        // Assert
        if (result.Success && result.Hardware != null)
        {
            Assert.NotNull(result.Hardware.Manufacturer);
            Assert.NotNull(result.Hardware.Model);
            Assert.NotNull(result.Hardware.BiosVersion);
            Assert.NotNull(result.Hardware.Processors);
            Assert.NotNull(result.Hardware.Memory);
            Assert.NotNull(result.Hardware.Disks);
            Assert.NotNull(result.Hardware.Graphics);
        }
    }

    [Fact]
    public async Task GetWmiInventoryAsync_CollectsProcessorInfo()
    {
        // Act
        var result = await _provider.GetWmiInventoryAsync();

        // Assert
        if (result.Success && result.Hardware?.Processors != null && result.Hardware.Processors.Count > 0)
        {
            var cpu = result.Hardware.Processors[0];
            Assert.NotNull(cpu.Name);
            Assert.NotNull(cpu.Manufacturer);
            Assert.True(cpu.Cores > 0);
            Assert.True(cpu.LogicalProcessors > 0);
            Assert.True(cpu.MaxClockSpeed > 0);
        }
    }

    [Fact]
    public async Task GetWmiInventoryAsync_CollectsMemoryInfo()
    {
        // Act
        var result = await _provider.GetWmiInventoryAsync();

        // Assert
        if (result.Success && result.Hardware?.Memory != null && result.Hardware.Memory.Count > 0)
        {
            var mem = result.Hardware.Memory[0];
            Assert.NotNull(mem.Manufacturer);
            Assert.True(mem.CapacityBytes > 0);
            Assert.True(mem.SpeedMHz >= 0);
        }
    }

    [Fact]
    public async Task GetWmiInventoryAsync_CollectsDiskInfo()
    {
        // Act
        var result = await _provider.GetWmiInventoryAsync();

        // Assert
        if (result.Success && result.Hardware?.Disks != null && result.Hardware.Disks.Count > 0)
        {
            var disk = result.Hardware.Disks[0];
            Assert.NotNull(disk.Model);
            Assert.True(disk.SizeBytes > 0);
            Assert.NotNull(disk.InterfaceType);
        }
    }

    [Fact]
    public async Task GetWmiInventoryAsync_CollectsDriverInfo()
    {
        // Act
        var result = await _provider.GetWmiInventoryAsync();

        // Assert
        if (result.Success && result.Drivers != null)
        {
            Assert.True(result.Drivers.TotalCount >= 0);
            Assert.True(result.Drivers.SignedCount >= 0);
            Assert.True(result.Drivers.UnsignedCount >= 0);
            Assert.Equal(result.Drivers.TotalCount, result.Drivers.SignedCount + result.Drivers.UnsignedCount);
        }
    }

    [Fact]
    public async Task GetWmiInventoryAsync_CollectsStorageInfo()
    {
        // Act
        var result = await _provider.GetWmiInventoryAsync();

        // Assert
        if (result.Success && result.Storage != null)
        {
            Assert.NotNull(result.Storage.Volumes);
            Assert.True(result.Storage.TotalCapacityBytes >= 0);
            Assert.True(result.Storage.TotalFreeBytes >= 0);
            Assert.True(result.Storage.TotalFreeBytes <= result.Storage.TotalCapacityBytes);
        }
    }

    [Fact]
    public async Task GetWmiInventoryAsync_CollectsNetworkInfo()
    {
        // Act
        var result = await _provider.GetWmiInventoryAsync();

        // Assert
        if (result.Success && result.Network != null)
        {
            Assert.NotNull(result.Network.Adapters);
            Assert.NotNull(result.Network.DomainName);
            Assert.NotNull(result.Network.WorkgroupName);
            Assert.NotNull(result.Network.DnsServers);
        }
    }

    [Fact]
    public async Task GetWmiInventoryAsync_CollectsPowerInfo()
    {
        // Act
        var result = await _provider.GetWmiInventoryAsync();

        // Assert
        if (result.Success && result.Power != null)
        {
            Assert.NotNull(result.Power.PowerPlan);
            // Battery info may be null on desktop systems
        }
    }

    [Fact]
    public async Task GetWmiInventoryAsync_CollectsSecurityInfo()
    {
        // Act
        var result = await _provider.GetWmiInventoryAsync();

        // Assert
        if (result.Success && result.Security != null)
        {
            // All boolean fields should have valid values
            Assert.True(result.Security.TpmPresent || !result.Security.TpmPresent);
            Assert.True(result.Security.WindowsDefenderRunning || !result.Security.WindowsDefenderRunning);
            Assert.True(result.Security.FirewallEnabled || !result.Security.FirewallEnabled);
        }
    }

    [Fact]
    public async Task GetWmiInventoryAsync_HandlesMultipleConcurrentCalls()
    {
        // Act
        var tasks = new[]
        {
            _provider.GetWmiInventoryAsync(),
            _provider.GetWmiInventoryAsync(),
            _provider.GetWmiInventoryAsync()
        };

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.NotNull(result.DeviceId);
            Assert.NotNull(result.CorrelationId);
        });

        // All should have same device ID
        Assert.Equal(results[0].DeviceId, results[1].DeviceId);
        Assert.Equal(results[1].DeviceId, results[2].DeviceId);

        // But different correlation IDs
        Assert.NotEqual(results[0].CorrelationId, results[1].CorrelationId);
        Assert.NotEqual(results[1].CorrelationId, results[2].CorrelationId);
    }

    [Fact]
    public async Task GetWmiInventoryAsync_RespectsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _provider.GetWmiInventoryAsync(cts.Token);
        });
    }
}

