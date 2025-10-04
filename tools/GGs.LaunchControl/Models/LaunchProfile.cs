using System.Text.Json.Serialization;

namespace GGs.LaunchControl.Models;

/// <summary>
/// Defines a launch profile for orchestrating application startup.
/// </summary>
public sealed class LaunchProfile
{
    /// <summary>
    /// Profile name (e.g., "desktop", "errorlogviewer", "fusion").
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// Applications to launch in this profile.
    /// </summary>
    [JsonPropertyName("applications")]
    public required List<ApplicationDefinition> Applications { get; init; }

    /// <summary>
    /// Health checks to perform before launching.
    /// </summary>
    [JsonPropertyName("healthChecks")]
    public List<HealthCheck> HealthChecks { get; init; } = new();

    /// <summary>
    /// Exit policy (e.g., "WaitForAll", "WaitForAny", "FireAndForget").
    /// </summary>
    [JsonPropertyName("exitPolicy")]
    public string ExitPolicy { get; init; } = "FireAndForget";

    /// <summary>
    /// Whether this profile requires elevation.
    /// </summary>
    [JsonPropertyName("requiresElevation")]
    public bool RequiresElevation { get; init; } = false;

    /// <summary>
    /// Startup delay in milliseconds between launching applications.
    /// </summary>
    [JsonPropertyName("startupDelayMs")]
    public int StartupDelayMs { get; init; } = 1000;
}

/// <summary>
/// Defines an application to launch.
/// </summary>
public sealed class ApplicationDefinition
{
    /// <summary>
    /// Application name for display.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Relative path to executable from LaunchControl directory.
    /// </summary>
    [JsonPropertyName("executablePath")]
    public required string ExecutablePath { get; init; }

    /// <summary>
    /// Command-line arguments.
    /// </summary>
    [JsonPropertyName("arguments")]
    public string? Arguments { get; init; }

    /// <summary>
    /// Working directory (defaults to executable directory).
    /// </summary>
    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Whether this application requires elevation.
    /// </summary>
    [JsonPropertyName("requiresElevation")]
    public bool RequiresElevation { get; init; } = false;

    /// <summary>
    /// Whether to wait for this application to exit before continuing.
    /// </summary>
    [JsonPropertyName("waitForExit")]
    public bool WaitForExit { get; init; } = false;

    /// <summary>
    /// Timeout in seconds for waiting (0 = infinite).
    /// </summary>
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; init; } = 0;

    /// <summary>
    /// Whether this application is optional (failure doesn't stop profile).
    /// </summary>
    [JsonPropertyName("optional")]
    public bool Optional { get; init; } = false;
}

/// <summary>
/// Defines a health check to perform before launching.
/// </summary>
public sealed class HealthCheck
{
    /// <summary>
    /// Check type (e.g., "FileExists", "DirectoryExists", "PortAvailable", "DotNetRuntime").
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Check target (e.g., file path, port number).
    /// </summary>
    [JsonPropertyName("target")]
    public required string Target { get; init; }

    /// <summary>
    /// Human-readable description of what this check validates.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// Whether this check is required (failure stops launch).
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; init; } = true;

    /// <summary>
    /// Auto-fix action if check fails (e.g., "CreateDirectory", "DownloadFile").
    /// </summary>
    [JsonPropertyName("autoFix")]
    public string? AutoFix { get; init; }
}

/// <summary>
/// Result of a health check.
/// </summary>
public sealed class HealthCheckResult
{
    public required HealthCheck Check { get; init; }
    public required bool Passed { get; init; }
    public string? Message { get; init; }
    public bool AutoFixed { get; init; } = false;
}

/// <summary>
/// Result of launching an application.
/// </summary>
public sealed class LaunchResult
{
    public required ApplicationDefinition Application { get; init; }
    public required bool Success { get; init; }
    public int? ProcessId { get; init; }
    public string? ErrorMessage { get; init; }
    public bool ElevationDeclined { get; init; } = false;
    public DateTime LaunchedAtUtc { get; init; } = DateTime.UtcNow;
}

