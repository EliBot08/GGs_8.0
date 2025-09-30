using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using GGs.Desktop.Telemetry;
using GGs.Shared.Enums;
using GGs.Shared.Tweaks;
using Xunit;

namespace GGs.E2ETests;

public class OpenTelemetrySmokeTests
{
    [Fact]
    public async Task LicenseOperations_ShouldEmitActivities()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("GGs.Desktop.License", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => activities.Add(a),
            ActivityStopped = a => { }
        };
        ActivitySource.AddActivityListener(listener);

        var temp = Path.Combine(Path.GetTempPath(), "ggs_otel_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_DATA_DIR", temp);
        try
        {
            var svc = new LicenseService();
            var res = await svc.ValidateAndSaveFromTextAsync("1234567890ABCDEF");
            Assert.True(res.ok);

            Assert.True(svc.TryLoadAndValidate(out var loaded, out var _));
            Assert.NotNull(loaded);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
            Environment.SetEnvironmentVariable("GGS_DATA_DIR", null);
        }

        Assert.NotEmpty(activities);
        Assert.True(
            activities.Exists(a => a.OperationName.Contains("license.validate_save", StringComparison.OrdinalIgnoreCase)) ||
            activities.Exists(a => a.OperationName.Contains("license.try_load_validate", StringComparison.OrdinalIgnoreCase)),
            "Expected a license-related activity (validate_save or try_load_validate).");
    }

    [Fact]
    public async Task TweakExecution_ShouldEmitActivities()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("GGs.Agent.Tweak", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => activities.Add(a),
            ActivityStopped = a => { }
        };
        ActivitySource.AddActivityListener(listener);

        var executor = new TweakExecutionService();
        var tweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Otel Test Registry",
            CommandType = CommandType.Registry,
            RegistryPath = @"HKEY_CURRENT_USER\\Software\\GGs\\OtelTest",
            RegistryValueName = "Value",
            RegistryValueType = "String",
            RegistryValueData = "Data",
            AllowUndo = true,
            Safety = SafetyLevel.Low,
            Risk = RiskLevel.Low
        };

        var log = await executor.ExecuteTweakAsync(tweak);
        Assert.NotNull(log);

        Assert.NotEmpty(activities);
        Assert.Contains(activities, a => a.OperationName.Contains("tweak.execute", StringComparison.OrdinalIgnoreCase));
    }
}
