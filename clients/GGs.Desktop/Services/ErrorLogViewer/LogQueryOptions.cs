using System;
using System.Collections.Generic;

namespace GGs.Desktop.Services.ErrorLogViewer;

public sealed class LogQueryOptions
{
    public IReadOnlyCollection<string> Levels { get; init; } = Array.Empty<string>();
    public string SearchText { get; init; } = string.Empty;
    public int Skip { get; init; }
    public int Take { get; init; } = 500;
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}
