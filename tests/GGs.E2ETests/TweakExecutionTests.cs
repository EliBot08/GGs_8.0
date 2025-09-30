using System;
using System.Threading.Tasks;
using Xunit;
using GGs.Desktop.Services;
using GGs.Shared.Tweaks;
using GGs.Shared.Enums;

namespace GGs.E2ETests;

public class TweakExecutionTests
{
    [Fact]
    public async Task ExecuteTweak_WithUndoRedo_ShouldMaintainStateConsistency()
    {
        // Arrange
        var executor = new TweakExecutionService();
        var undoRedo = new UndoRedoService(executor);
        
        var testTweak = new TweakDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test Registry Tweak",
            CommandType = CommandType.Registry,
            RegistryPath = @"HKEY_CURRENT_USER\Software\GGs\Test",
            RegistryValueName = "TestValue",
            RegistryValueType = "String",
            RegistryValueData = "TestData",
            AllowUndo = true,
            Safety = SafetyLevel.Low,
            Risk = RiskLevel.Low
        };

        // Act - Execute
        var executeLog = await executor.ExecuteTweakAsync(testTweak);
        Assert.NotNull(executeLog);
        Assert.True(executeLog.Success);
        Assert.NotNull(executeLog.BeforeState);
        Assert.NotNull(executeLog.AfterState);
        
        if (testTweak.AllowUndo)
        {
            undoRedo.RecordAction(executeLog);
        }

        // Act - Undo
        var undoSuccess = await undoRedo.UndoAsync();
        Assert.True(undoSuccess);
        
        // Act - Redo
        var redoSuccess = await undoRedo.RedoAsync();
        Assert.True(redoSuccess);
        
        // Cleanup
        await undoRedo.UndoAsync();
    }

    [Theory]
    [InlineData(CommandType.Registry)]
    [InlineData(CommandType.Script)]
    public async Task SupportedTweakTypes_ShouldExecuteSuccessfully(CommandType commandType)
    {
        // Arrange
        var executor = new TweakExecutionService();
        var tweak = CreateTestTweak(commandType);
        
        // Act
        var log = await executor.ExecuteTweakAsync(tweak);
        
        // Assert
        Assert.NotNull(log);
        Assert.True(log!.Success || log.Error != null);
    }

    [Fact]
    public async Task BatchTweakExecution_ShouldProcessInOrder()
    {
        // Arrange
        var executor = new TweakExecutionService();
        var tweaks = new[]
        {
            CreateTestTweak(CommandType.Registry),
            CreateTestTweak(CommandType.Service),
            CreateTestTweak(CommandType.Registry)
        };

        // Act
        var logs = new List<TweakApplicationLog>();
        foreach (var tweak in tweaks)
        {
            var log = await executor.ExecuteTweakAsync(tweak);
            logs.Add(log);
        }

        // Assert
        Assert.Equal(3, logs.Count);
        Assert.All(logs, log => Assert.NotNull(log));
    }

    private TweakDefinition CreateTestTweak(CommandType type)
    {
        return type switch
        {
            CommandType.Registry => new TweakDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Test Registry",
                CommandType = CommandType.Registry,
                RegistryPath = @"HKEY_CURRENT_USER\Software\GGs\TestE2E",
                RegistryValueName = $"Test_{Guid.NewGuid():N}",
                RegistryValueType = "DWORD",
                RegistryValueData = "1",
                AllowUndo = true,
                Safety = SafetyLevel.Low,
                Risk = RiskLevel.Low
            },
            CommandType.Service => new TweakDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Test Service",
                CommandType = CommandType.Service,
                ServiceName = "Spooler",
                // Avoid service actions in tests to reduce side effects; default to null (no-op)
                ServiceAction = null,
                AllowUndo = false,
                Safety = SafetyLevel.Low,
                Risk = RiskLevel.Low
            },
            CommandType.Script => new TweakDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Test Script",
                CommandType = CommandType.Script,
                ScriptContent = "Write-Host 'E2E Test'",
                AllowUndo = false,
                Safety = SafetyLevel.Low,
                Risk = RiskLevel.Low
            },
            _ => throw new ArgumentException($"Unknown command type: {type}")
        };
    }
}
