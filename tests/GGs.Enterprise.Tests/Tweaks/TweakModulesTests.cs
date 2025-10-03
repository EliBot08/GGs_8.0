using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using GGs.Agent.Tweaks;
using GGs.Shared.Tweaks;
using GGs.Shared.Enums;

namespace GGs.Enterprise.Tests.Tweaks;

public class RegistryTweakModuleTests
{
    private readonly ILogger<RegistryTweakModule> _logger;
    private readonly RegistryTweakModule _module;

    public RegistryTweakModuleTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<RegistryTweakModule>();
        _module = new RegistryTweakModule(_logger);
    }

    [Fact]
    public void ModuleName_ReturnsRegistry()
    {
        Assert.Equal("Registry", _module.ModuleName);
    }

    [Fact]
    public async Task PreflightAsync_WithNullPath_ReturnsFalse()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            CommandType = CommandType.Registry,
            RegistryPath = null,
            RegistryValueName = "TestValue"
        };

        var result = await _module.PreflightAsync(tweak);

        Assert.False(result.CanApply);
        Assert.Contains("required", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreflightAsync_WithInvalidRoot_ReturnsFalse()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            CommandType = CommandType.Registry,
            RegistryPath = @"HKCR\Software\Test",
            RegistryValueName = "TestValue"
        };

        var result = await _module.PreflightAsync(tweak);

        Assert.False(result.CanApply);
        Assert.NotNull(result.PolicyViolation);
    }

    [Fact]
    public async Task PreflightAsync_WithBlockedPath_ReturnsFalse()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            CommandType = CommandType.Registry,
            RegistryPath = @"HKLM\SYSTEM\CurrentControlSet\Services\WinDefend",
            RegistryValueName = "Start"
        };

        var result = await _module.PreflightAsync(tweak);

        Assert.False(result.CanApply);
        Assert.NotNull(result.PolicyViolation);
    }

    [Fact]
    public async Task PreflightAsync_WithValidPath_ReturnsTrue()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            CommandType = CommandType.Registry,
            RegistryPath = @"HKCU\Software\GGsTest",
            RegistryValueName = "TestValue",
            RegistryValueType = "String",
            RegistryValueData = "TestData"
        };

        var result = await _module.PreflightAsync(tweak);

        Assert.True(result.CanApply);
        Assert.NotNull(result.BeforeState);
    }
}

public class ServiceTweakModuleTests
{
    private readonly ILogger<ServiceTweakModule> _logger;
    private readonly ServiceTweakModule _module;

    public ServiceTweakModuleTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ServiceTweakModule>();
        _module = new ServiceTweakModule(_logger);
    }

    [Fact]
    public void ModuleName_ReturnsService()
    {
        Assert.Equal("Service", _module.ModuleName);
    }

    [Fact]
    public async Task PreflightAsync_WithNullServiceName_ReturnsFalse()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            CommandType = CommandType.Service,
            ServiceName = null,
            ServiceAction = ServiceAction.Start
        };

        var result = await _module.PreflightAsync(tweak);

        Assert.False(result.CanApply);
        Assert.Contains("required", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreflightAsync_WithCriticalServiceStop_ReturnsFalse()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            CommandType = CommandType.Service,
            ServiceName = "WinDefend",
            ServiceAction = ServiceAction.Stop
        };

        var result = await _module.PreflightAsync(tweak);

        Assert.False(result.CanApply);
        Assert.NotNull(result.PolicyViolation);
        Assert.Contains("critical", result.PolicyViolation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreflightAsync_WithNonExistentService_ReturnsFalse()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            CommandType = CommandType.Service,
            ServiceName = "NonExistentService12345",
            ServiceAction = ServiceAction.Start
        };

        var result = await _module.PreflightAsync(tweak);

        Assert.False(result.CanApply);
        Assert.Contains("not found", result.Reason, StringComparison.OrdinalIgnoreCase);
    }
}

public class NetworkTweakModuleTests
{
    private readonly ILogger<NetworkTweakModule> _logger;
    private readonly NetworkTweakModule _module;

    public NetworkTweakModuleTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<NetworkTweakModule>();
        _module = new NetworkTweakModule(_logger);
    }

    [Fact]
    public void ModuleName_ReturnsNetwork()
    {
        Assert.Equal("Network", _module.ModuleName);
    }

    [Fact]
    public async Task PreflightAsync_ReturnsValidResult()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test Network Config",
            CommandType = CommandType.Registry // Using Registry as placeholder
        };

        var result = await _module.PreflightAsync(tweak);

        Assert.NotNull(result);
        Assert.NotNull(result.BeforeState);
    }

    [Fact]
    public async Task ApplyAsync_ReturnsSuccess()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test Network Config",
            CommandType = CommandType.Registry
        };

        var result = await _module.ApplyAsync(tweak);

        Assert.True(result.Success);
        Assert.NotNull(result.BeforeState);
        Assert.NotNull(result.AfterState);
    }

    [Fact]
    public async Task VerifyAsync_ChecksConnectivity()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test Network Config",
            CommandType = CommandType.Registry
        };

        var result = await _module.VerifyAsync(tweak);

        Assert.NotNull(result);
        Assert.NotNull(result.CurrentState);
    }
}

public class PowerPerformanceTweakModuleTests
{
    private readonly ILogger<PowerPerformanceTweakModule> _logger;
    private readonly PowerPerformanceTweakModule _module;

    public PowerPerformanceTweakModuleTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PowerPerformanceTweakModule>();
        _module = new PowerPerformanceTweakModule(_logger);
    }

    [Fact]
    public void ModuleName_ReturnsPowerPerformance()
    {
        Assert.Equal("PowerPerformance", _module.ModuleName);
    }

    [Fact]
    public async Task PreflightAsync_ReturnsValidResult()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Set High Performance",
            CommandType = CommandType.Registry
        };

        var result = await _module.PreflightAsync(tweak);

        Assert.NotNull(result);
        Assert.NotNull(result.BeforeState);
    }

    [Fact]
    public async Task ApplyAsync_ReturnsSuccess()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Set Balanced Power Plan",
            CommandType = CommandType.Registry
        };

        var result = await _module.ApplyAsync(tweak);

        Assert.True(result.Success);
        Assert.NotNull(result.BeforeState);
        Assert.NotNull(result.AfterState);
    }
}

public class SecurityHealthTweakModuleTests
{
    private readonly ILogger<SecurityHealthTweakModule> _logger;
    private readonly SecurityHealthTweakModule _module;

    public SecurityHealthTweakModuleTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<SecurityHealthTweakModule>();
        _module = new SecurityHealthTweakModule(_logger);
    }

    [Fact]
    public void ModuleName_ReturnsSecurityHealth()
    {
        Assert.Equal("SecurityHealth", _module.ModuleName);
    }

    [Fact]
    public async Task PreflightAsync_WithDisableDefender_ReturnsFalse()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Disable Defender",
            CommandType = CommandType.Registry
        };

        var result = await _module.PreflightAsync(tweak);

        Assert.False(result.CanApply);
        Assert.NotNull(result.PolicyViolation);
    }

    [Fact]
    public async Task ApplyAsync_ReturnsSuccess()
    {
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Check Security Health",
            CommandType = CommandType.Registry
        };

        var result = await _module.ApplyAsync(tweak);

        Assert.True(result.Success);
        Assert.NotNull(result.BeforeState);
        Assert.NotNull(result.AfterState);
    }
}

