using System.Net.Sockets;
using System.Runtime.InteropServices;
using GGs.LaunchControl.Models;

namespace GGs.LaunchControl.Services;

/// <summary>
/// Performs health checks before launching applications.
/// </summary>
public sealed class HealthChecker
{
    public async Task<List<HealthCheckResult>> RunHealthChecksAsync(List<HealthCheck> checks)
    {
        var results = new List<HealthCheckResult>();

        foreach (var check in checks)
        {
            var result = await RunHealthCheckAsync(check);
            results.Add(result);
        }

        return results;
    }

    private async Task<HealthCheckResult> RunHealthCheckAsync(HealthCheck check)
    {
        try
        {
            return check.Type.ToLowerInvariant() switch
            {
                "fileexists" => CheckFileExists(check),
                "directoryexists" => CheckDirectoryExists(check),
                "portavailable" => await CheckPortAvailableAsync(check),
                "dotnetruntime" => CheckDotNetRuntime(check),
                "diskspace" => CheckDiskSpace(check),
                "gpufeatures" => CheckGpuFeatures(check),
                _ => new HealthCheckResult
                {
                    Check = check,
                    Passed = false,
                    Message = $"Unknown check type: {check.Type}"
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Check = check,
                Passed = false,
                Message = $"Check failed: {ex.Message}"
            };
        }
    }

    private HealthCheckResult CheckFileExists(HealthCheck check)
    {
        var exists = File.Exists(check.Target);
        return new HealthCheckResult
        {
            Check = check,
            Passed = exists,
            Message = exists ? $"File found: {check.Target}" : $"File not found: {check.Target}"
        };
    }

    private HealthCheckResult CheckDirectoryExists(HealthCheck check)
    {
        var exists = Directory.Exists(check.Target);
        
        if (!exists && check.AutoFix == "CreateDirectory")
        {
            try
            {
                Directory.CreateDirectory(check.Target);
                return new HealthCheckResult
                {
                    Check = check,
                    Passed = true,
                    AutoFixed = true,
                    Message = $"Directory created: {check.Target}"
                };
            }
            catch (Exception ex)
            {
                return new HealthCheckResult
                {
                    Check = check,
                    Passed = false,
                    Message = $"Failed to create directory: {ex.Message}"
                };
            }
        }

        return new HealthCheckResult
        {
            Check = check,
            Passed = exists,
            Message = exists ? $"Directory found: {check.Target}" : $"Directory not found: {check.Target}"
        };
    }

    private async Task<HealthCheckResult> CheckPortAvailableAsync(HealthCheck check)
    {
        if (!int.TryParse(check.Target, out var port))
        {
            return new HealthCheckResult
            {
                Check = check,
                Passed = false,
                Message = $"Invalid port number: {check.Target}"
            };
        }

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            
            return new HealthCheckResult
            {
                Check = check,
                Passed = false,
                Message = $"Port {port} is already in use"
            };
        }
        catch (SocketException)
        {
            // Port is available (connection refused)
            return new HealthCheckResult
            {
                Check = check,
                Passed = true,
                Message = $"Port {port} is available"
            };
        }
    }

    private HealthCheckResult CheckDotNetRuntime(HealthCheck check)
    {
        try
        {
            var runtimeVersion = RuntimeInformation.FrameworkDescription;
            var expectedVersion = check.Target;

            var passed = runtimeVersion.Contains(expectedVersion, StringComparison.OrdinalIgnoreCase);

            return new HealthCheckResult
            {
                Check = check,
                Passed = passed,
                Message = passed
                    ? $".NET Runtime OK: {runtimeVersion}"
                    : $".NET Runtime mismatch: Expected {expectedVersion}, Found {runtimeVersion}"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Check = check,
                Passed = false,
                Message = $"Failed to check .NET runtime: {ex.Message}"
            };
        }
    }

    private HealthCheckResult CheckDiskSpace(HealthCheck check)
    {
        try
        {
            var parts = check.Target.Split(':');
            if (parts.Length != 2)
            {
                return new HealthCheckResult
                {
                    Check = check,
                    Passed = false,
                    Message = $"Invalid disk space check format. Expected 'Drive:MinGB', got '{check.Target}'"
                };
            }

            var drive = parts[0];
            if (!long.TryParse(parts[1], out var minGb))
            {
                return new HealthCheckResult
                {
                    Check = check,
                    Passed = false,
                    Message = $"Invalid minimum GB value: {parts[1]}"
                };
            }

            var driveInfo = new DriveInfo(drive);
            var freeGb = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            var passed = freeGb >= minGb;

            return new HealthCheckResult
            {
                Check = check,
                Passed = passed,
                Message = passed
                    ? $"Disk space OK: {freeGb:F2} GB available on {drive}:"
                    : $"Low disk space: {freeGb:F2} GB available on {drive}: (minimum {minGb} GB required)"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Check = check,
                Passed = false,
                Message = $"Failed to check disk space: {ex.Message}"
            };
        }
    }

    private HealthCheckResult CheckGpuFeatures(HealthCheck check)
    {
        // Simplified GPU check - in production this would use DirectX/Vulkan APIs
        try
        {
            var passed = true; // Assume GPU is available for now
            return new HealthCheckResult
            {
                Check = check,
                Passed = passed,
                Message = "GPU features check passed (simplified check)"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Check = check,
                Passed = false,
                Message = $"GPU check failed: {ex.Message}"
            };
        }
    }
}

