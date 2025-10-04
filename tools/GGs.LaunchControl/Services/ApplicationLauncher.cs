using System.ComponentModel;
using System.Diagnostics;
using GGs.LaunchControl.Models;

namespace GGs.LaunchControl.Services;

/// <summary>
/// Launches applications with proper error handling and elevation support.
/// </summary>
public sealed class ApplicationLauncher
{
    private readonly bool _elevationRequested;

    public ApplicationLauncher(bool elevationRequested)
    {
        _elevationRequested = elevationRequested;
    }

    public async Task<List<LaunchResult>> LaunchApplicationsAsync(LaunchProfile profile, string mode)
    {
        var results = new List<LaunchResult>();

        foreach (var app in profile.Applications)
        {
            // Apply startup delay between applications
            if (results.Count > 0 && profile.StartupDelayMs > 0)
            {
                await Task.Delay(profile.StartupDelayMs);
            }

            var result = await LaunchApplicationAsync(app, mode);
            results.Add(result);

            // Stop if a required application failed to launch
            if (!result.Success && !app.Optional)
            {
                break;
            }
        }

        return results;
    }

    private async Task<LaunchResult> LaunchApplicationAsync(ApplicationDefinition app, string mode)
    {
        try
        {
            // Resolve executable path
            var exePath = ResolveExecutablePath(app.ExecutablePath);
            if (!File.Exists(exePath))
            {
                return new LaunchResult
                {
                    Application = app,
                    Success = false,
                    ErrorMessage = $"Executable not found: {exePath}"
                };
            }

            // Prepare arguments
            var arguments = app.Arguments ?? string.Empty;
            if (mode != "normal")
            {
                arguments = $"{arguments} --mode {mode}".Trim();
            }

            // Determine working directory
            var workingDir = string.IsNullOrWhiteSpace(app.WorkingDirectory)
                ? Path.GetDirectoryName(exePath)
                : ResolveExecutablePath(app.WorkingDirectory);

            // Prepare process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            // Handle elevation
            var requiresElevation = app.RequiresElevation || _elevationRequested;
            if (requiresElevation && !PrivilegeChecker.IsElevated())
            {
                startInfo.Verb = "runas";
            }

            // Launch process
            var process = Process.Start(startInfo);
            
            if (process == null)
            {
                return new LaunchResult
                {
                    Application = app,
                    Success = false,
                    ErrorMessage = "Failed to start process (Process.Start returned null)"
                };
            }

            // Wait for exit if requested
            if (app.WaitForExit)
            {
                var timeout = app.TimeoutSeconds > 0
                    ? TimeSpan.FromSeconds(app.TimeoutSeconds)
                    : Timeout.InfiniteTimeSpan;

                var exited = await WaitForExitAsync(process, timeout);
                
                if (!exited)
                {
                    return new LaunchResult
                    {
                        Application = app,
                        Success = false,
                        ProcessId = process.Id,
                        ErrorMessage = $"Process did not exit within {app.TimeoutSeconds} seconds"
                    };
                }

                return new LaunchResult
                {
                    Application = app,
                    Success = process.ExitCode == 0,
                    ProcessId = process.Id,
                    ErrorMessage = process.ExitCode != 0 ? $"Process exited with code {process.ExitCode}" : null
                };
            }

            // Give process a moment to start
            await Task.Delay(500);

            // Verify process is still running
            if (process.HasExited)
            {
                return new LaunchResult
                {
                    Application = app,
                    Success = false,
                    ProcessId = process.Id,
                    ErrorMessage = $"Process exited immediately with code {process.ExitCode}"
                };
            }

            return new LaunchResult
            {
                Application = app,
                Success = true,
                ProcessId = process.Id
            };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED (1223): The operation was canceled by the user (UAC declined)
            return new LaunchResult
            {
                Application = app,
                Success = false,
                ElevationDeclined = true,
                ErrorMessage = "ADMIN ACCESS DECLINED BY OPERATOR (expected, continuing non-elevated path)"
            };
        }
        catch (Exception ex)
        {
            return new LaunchResult
            {
                Application = app,
                Success = false,
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
    }

    private string ResolveExecutablePath(string relativePath)
    {
        // If path is absolute, return as-is
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }

        // Resolve relative to current working directory (where the batch script was run from)
        var baseDir = Directory.GetCurrentDirectory();
        var resolvedPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));

        return resolvedPath;
    }

    private async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            await process.WaitForExitAsync();
            return true;
        }

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

