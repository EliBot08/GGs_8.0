using System.Diagnostics;

namespace GGs.Desktop.Telemetry;

internal static class TelemetrySources
{
    public static readonly ActivitySource Desktop = new("GGs.Desktop");
    public static readonly ActivitySource Startup = new("GGs.Desktop.Startup");
    public static readonly ActivitySource License = new("GGs.Desktop.License");
    public static readonly ActivitySource Tweak = new("GGs.Agent.Tweak");
    public static readonly ActivitySource Log = new("GGs.Desktop.Log");
}
