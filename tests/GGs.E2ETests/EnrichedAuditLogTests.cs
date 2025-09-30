using System;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using GGs.Shared.Enums;
using GGs.Shared.Tweaks;
using Xunit;

namespace GGs.E2ETests
{
    public class EnrichedAuditLogTests
    {
        [Fact]
        public async Task RegistryTweak_Logs_EnrichedFields()
        {
            var executor = new TweakExecutionService();
            var tweak = new TweakDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Enriched Registry",
                CommandType = CommandType.Registry,
                RegistryPath = @"HKEY_CURRENT_USER\\Software\\GGs\\Enriched",
                RegistryValueName = $"Val_{Guid.NewGuid():N}",
                RegistryValueType = "String",
                RegistryValueData = "Hello",
                AllowUndo = true
            };

            var log = await executor.ExecuteTweakAsync(tweak);
            Assert.NotNull(log);
            Assert.True(log!.Success, log?.Error);
            Assert.Equal(tweak.RegistryPath, log.RegistryPath);
            Assert.Equal(tweak.RegistryValueName, log.RegistryValueName);
            Assert.Equal("String", log.RegistryValueType);
            Assert.NotNull(log.OriginalValue);
            Assert.Equal("Hello", log.NewValue);
        }

        [Fact]
        public async Task ScriptTweak_Logs_ScriptAppliedAndUndo()
        {
            var executor = new TweakExecutionService();
            var tweak = new TweakDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Enriched Script",
                CommandType = CommandType.Script,
                ScriptContent = "Write-Host 'Audit'",
                UndoScriptContent = "Write-Host 'Undo'",
                AllowUndo = true
            };

            var log = await executor.ExecuteTweakAsync(tweak);
            Assert.NotNull(log);
            Assert.True(log!.Success || log.Error != null);
            Assert.Equal(tweak.ScriptContent, log.ScriptApplied);
            Assert.Equal(tweak.UndoScriptContent, log.UndoScript);
        }
    }
}

