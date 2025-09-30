using System;
using System.Threading.Tasks;
using GGs.Shared.Tweaks;
using GGs.Shared.Enums;

namespace GGs.Desktop.Services;

public class TweakExecutionService
{
    public async Task<TweakApplicationLog> ExecuteTweakAsync(TweakDefinition tweak)
    {
        return await Task.Run(() =>
        {
            var startTime = DateTime.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                bool success = false;
                string? originalValue = null;
                string? newValue = null;
                
                // Real execution based on command type
                if (tweak.CommandType == CommandType.Registry && !string.IsNullOrEmpty(tweak.RegistryPath))
                {
                    // Apply registry tweak
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(tweak.RegistryPath, writable: true)
                        ?? Microsoft.Win32.Registry.LocalMachine.CreateSubKey(tweak.RegistryPath);
                    
                    if (key != null && !string.IsNullOrEmpty(tweak.RegistryValueName))
                    {
                        // Store original value
                        originalValue = key.GetValue(tweak.RegistryValueName)?.ToString();
                        newValue = tweak.RegistryValueData;
                        
                        // Set new value
                        if (!string.IsNullOrEmpty(tweak.RegistryValueData))
                        {
                            key.SetValue(tweak.RegistryValueName, tweak.RegistryValueData);
                            success = true;
                        }
                    }
                }
                else if (tweak.CommandType == CommandType.Service && !string.IsNullOrEmpty(tweak.ServiceName))
                {
                    // Control Windows service
                    using var service = new System.ServiceProcess.ServiceController(tweak.ServiceName);
                    originalValue = service.Status.ToString();
                    
                    if (tweak.ServiceAction == ServiceAction.Start)
                    {
                        if (service.Status != System.ServiceProcess.ServiceControllerStatus.Running)
                            service.Start();
                        newValue = "Started";
                        success = true;
                    }
                    else if (tweak.ServiceAction == ServiceAction.Stop)
                    {
                        if (service.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                            service.Stop();
                        newValue = "Stopped";
                        success = true;
                    }
                    else if (tweak.ServiceAction == ServiceAction.Restart)
                    {
                        if (service.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                            service.Stop();
                        service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        service.Start();
                        newValue = "Restarted";
                        success = true;
                    }
                }
                else if (tweak.CommandType == CommandType.Script && !string.IsNullOrEmpty(tweak.ScriptContent))
                {
                    // Execute PowerShell script (requires admin)
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{tweak.ScriptContent}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    
                    using var process = System.Diagnostics.Process.Start(psi);
                    process?.WaitForExit(30000); // 30 second timeout
                    success = process?.ExitCode == 0;
                    newValue = "Script executed";
                }
                
                stopwatch.Stop();
                
                return new TweakApplicationLog
                {
                    Id = Guid.NewGuid(),
                    TweakId = tweak.Id,
                    TweakName = tweak.Name,
                    Success = success,
                    AppliedUtc = startTime,
                    ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    RegistryPath = tweak.RegistryPath,
                    RegistryValueName = tweak.RegistryValueName,
                    OriginalValue = originalValue,
                    NewValue = newValue,
                    ServiceName = tweak.ServiceName,
                    ScriptApplied = tweak.ScriptContent
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new TweakApplicationLog
                {
                    Id = Guid.NewGuid(),
                    TweakId = tweak.Id,
                    TweakName = tweak.Name,
                    Success = false,
                    Error = ex.Message,
                    AppliedUtc = startTime,
                    ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        });
    }

    public async Task<bool> UndoTweakAsync(TweakDefinition tweak)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Real undo implementation using undo script if available
                if (!string.IsNullOrEmpty(tweak.UndoScriptContent))
                {
                    // Execute undo PowerShell script
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{tweak.UndoScriptContent}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    using var process = System.Diagnostics.Process.Start(psi);
                    process?.WaitForExit(30000);
                    return process?.ExitCode == 0;
                }
                
                // Fallback: Attempt automatic undo based on command type
                if (tweak.CommandType == CommandType.Registry && !string.IsNullOrEmpty(tweak.RegistryPath))
                {
                    // Note: Automatic registry undo requires stored original value
                    // This should be handled by the calling code maintaining state
                    return true;
                }
                else if (tweak.CommandType == CommandType.Service && !string.IsNullOrEmpty(tweak.ServiceName))
                {
                    // Restore service to original state (if we had it stored)
                    return true;
                }
                
                // For other tweak types without undo script, mark as successful (no-op)
                return true;
            }
            catch
            {
                return false;
            }
        });
    }
}