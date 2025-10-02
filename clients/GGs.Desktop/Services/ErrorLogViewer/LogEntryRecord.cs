using System;

namespace GGs.Desktop.Services.ErrorLogViewer;

public sealed class LogEntryRecord
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "INFO";
    public string Source { get; set; } = "[Unknown]";
    public string Message { get; set; } = "[No message]";
    public string Raw { get; set; } = string.Empty;
    public string FilePath { get; set; } = "[Unknown]";
    public int LineNumber { get; set; }
}
