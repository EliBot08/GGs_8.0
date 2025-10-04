using System.Text.Json.Serialization;

namespace GGs.LaunchControl.Models;

/// <summary>
/// JSON serialization context for LaunchProfile to support trimming and AOT.
/// </summary>
[JsonSerializable(typeof(LaunchProfile))]
[JsonSerializable(typeof(ApplicationDefinition))]
[JsonSerializable(typeof(HealthCheck))]
[JsonSerializable(typeof(List<ApplicationDefinition>))]
[JsonSerializable(typeof(List<HealthCheck>))]
internal partial class LaunchProfileJsonContext : JsonSerializerContext
{
}

