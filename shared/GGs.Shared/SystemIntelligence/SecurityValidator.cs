using System.Threading.Tasks;
using GGs.Shared.Enums;

namespace GGs.Shared.SystemIntelligence;

public class SecurityValidator
{
    public async Task<bool> ValidateTweakAsync(DetectedTweak tweak)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Validate tweak has required fields
                if (string.IsNullOrEmpty(tweak.Name)) return false;
                if (string.IsNullOrEmpty(tweak.Category)) return false;
                
                // Validate registry tweaks
                if (tweak.CommandType == CommandType.Registry)
                {
                    if (string.IsNullOrEmpty(tweak.RegistryPath)) return false;
                    
                    // Validate registry path format
                    var validRoots = new[] { "HKEY_LOCAL_MACHINE", "HKEY_CURRENT_USER", "HKEY_CLASSES_ROOT", "HKEY_USERS", "HKLM", "HKCU", "HKCR", "HKU" };
                    if (!validRoots.Any(root => tweak.RegistryPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
                        return false;
                }
                
                // Validate service tweaks
                if (tweak.CommandType == CommandType.Service)
                {
                    if (string.IsNullOrEmpty(tweak.ServiceName)) return false;
                }
                
                // Validate script tweaks
                if (tweak.CommandType == CommandType.PowerShell || tweak.CommandType == CommandType.CommandLine)
                {
                    if (string.IsNullOrEmpty(tweak.Command)) return false;
                    
                    // Check for dangerous commands in the Command field
                    var dangerous = new[] { "format", "del /f /s /q", "rmdir /s /q", "rd /s /q", "Remove-Item -Recurse -Force" };
                    foreach (var cmd in dangerous)
                    {
                        if (tweak.Command.Contains(cmd, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<SafetyLevel> AssessSecurityRiskAsync(DetectedTweak tweak)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Assess based on tweak type
                if (tweak.CommandType == CommandType.PowerShell || tweak.CommandType == CommandType.CommandLine)
                {
                    // Scripts have higher risk
                    if (tweak.Command?.Contains("Set-ExecutionPolicy") == true) return SafetyLevel.Low;
                    if (tweak.Command?.Contains("Remove-Item") == true) return SafetyLevel.Medium;
                    if (tweak.Command?.Contains("Stop-Service") == true) return SafetyLevel.Medium;
                    return SafetyLevel.High;
                }
                
                if (tweak.CommandType == CommandType.Registry)
                {
                    // System registry paths are higher risk
                    if (tweak.RegistryPath?.Contains(@"SYSTEM\CurrentControlSet") == true) return SafetyLevel.Medium;
                    if (tweak.RegistryPath?.Contains(@"Windows\CurrentVersion\Run") == true) return SafetyLevel.Low;
                    if (tweak.RegistryPath?.Contains(@"Policies\System") == true) return SafetyLevel.Medium;
                    return SafetyLevel.High;
                }
                
                if (tweak.CommandType == CommandType.Service)
                {
                    // Service modifications are medium risk
                    var criticalServices = new[] { "wuauserv", "WinDefend", "wscsvc", "EventLog" };
                    if (criticalServices.Any(s => s.Equals(tweak.ServiceName, StringComparison.OrdinalIgnoreCase)))
                        return SafetyLevel.Low;
                    return SafetyLevel.Medium;
                }
                
                return SafetyLevel.High;
            }
            catch
            {
                return SafetyLevel.High;
            }
        });
    }

    public async Task<bool> IsTweakSafeAsync(DetectedTweak tweak)
    {
        try
        {
            // First validate the tweak structure
            if (!await ValidateTweakAsync(tweak))
                return false;
            
            // Then assess security risk using SafetyLevel
            var safety = await AssessSecurityRiskAsync(tweak);
            
            // Low safety level tweaks are not considered safe
            if (safety == SafetyLevel.Low)
                return false;
            
            // Additional safety checks
            if (tweak.CommandType == CommandType.Registry)
            {
                // Check for protected registry keys
                var protectedPaths = new[]
                {
                    @"SYSTEM\CurrentControlSet\Services\BIOS",
                    @"SYSTEM\CurrentControlSet\Control\Class\{4d36e967",
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
                };
                
                if (protectedPaths.Any(p => tweak.RegistryPath?.Contains(p, StringComparison.OrdinalIgnoreCase) == true))
                    return false;
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}
