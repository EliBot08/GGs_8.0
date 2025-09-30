using System;
using System.Threading.Tasks;
using Xunit;
using GGs.Desktop.Services;
using GGs.Shared.Tweaks;
using GGs.Shared.Enums;

namespace GGs.E2ETests;

public class Prompt30Tests
{
    [Fact]
    public async Task Script_Blocked_ShouldReturnPolicyMessage()
    {
        Environment.SetEnvironmentVariable("GGS_SCRIPTS_MODE", "moderate");
        var svc = new TweakExecutionService();
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Blocked Script",
            CommandType = CommandType.Script,
            ScriptContent = "Start-Process -Verb RunAs notepad.exe",
            AllowUndo = false,
            Safety = SafetyLevel.Low,
            Risk = RiskLevel.Low
        };
        var log = await svc.ExecuteTweakAsync(tweak);
        Assert.NotNull(log);
        Assert.False(log!.Success);
        Assert.Contains("blocked by policy", log.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Environment.SetEnvironmentVariable("GGS_SCRIPTS_MODE", null);
    }
}

