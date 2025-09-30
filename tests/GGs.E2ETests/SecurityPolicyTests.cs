using System;
using System.Threading.Tasks;
using GGs.Agent;
using GGs.Desktop.Services;
using GGs.Shared.Enums;
using GGs.Shared.Tweaks;
using Xunit;

namespace GGs.E2ETests;

public class SecurityPolicyTests
{
    [Fact]
    public void ScriptPolicy_ModerateMode_BlocksDangerousCommands()
    {
        Environment.SetEnvironmentVariable("GGS_SCRIPTS_MODE", null);
        Assert.False(InvokeIsAllowed("Remove-Item -Recurse -Force C:\\"));
        Assert.False(InvokeIsAllowed("Start-Process -Verb RunAs notepad.exe"));
        Assert.True(InvokeIsAllowed("Write-Host 'OK'"));
    }

    [Fact]
    public void ScriptPolicy_StrictMode_AllowsOnlySafePrefixes()
    {
        Environment.SetEnvironmentVariable("GGS_SCRIPTS_MODE", "strict");
        Assert.True(InvokeIsAllowed("Write-Host 'Hello'"));
        Assert.False(InvokeIsAllowed("Start-Process notepad.exe"));
        Environment.SetEnvironmentVariable("GGS_SCRIPTS_MODE", null);
    }

    [Fact]
    public async Task TweakExecutor_RegistryDisallowedRoot_IsBlocked()
    {
        var svc = new TweakExecutionService();
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Disallowed Root Test",
            CommandType = CommandType.Registry,
            RegistryPath = "HKEY_CLASSES_ROOT\\GGsTest",
            RegistryValueName = "Test",
            RegistryValueType = "String",
            RegistryValueData = "x",
            AllowUndo = false,
            Safety = SafetyLevel.Low,
            Risk = RiskLevel.Low
        };
        var log = await svc.ExecuteTweakAsync(tweak);
        Assert.NotNull(log);
        Assert.False(log!.Success);
        Assert.Contains("not allowed by policy", log.Error ?? string.Empty);
    }

    [Fact]
    public async Task TweakExecutor_ServiceBlocklist_Stop_Disallowed()
    {
        var svc = new TweakExecutionService();
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Stop Blocked Service",
            CommandType = CommandType.Service,
            ServiceName = "WinDefend",
            ServiceAction = ServiceAction.Stop,
            AllowUndo = false,
            Safety = SafetyLevel.Medium,
            Risk = RiskLevel.High
        };
        var log = await svc.ExecuteTweakAsync(tweak);
        Assert.NotNull(log);
        Assert.False(log!.Success);
        Assert.Contains("blocked by policy", log.Error ?? string.Empty);
    }

    private static bool InvokeIsAllowed(string script)
    {
        var method = typeof(ScriptPolicy).GetMethod("IsAllowed", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        Assert.NotNull(method);
        var res = (bool)method!.Invoke(null, new object?[] { script })!;
        return res;
    }
}

