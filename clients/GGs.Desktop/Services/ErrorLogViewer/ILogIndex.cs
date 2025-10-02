using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GGs.Desktop.Services.ErrorLogViewer;

public interface ILogIndex : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LogEntryRecord>> AddEntriesAsync(IEnumerable<LogEntryRecord> entries, CancellationToken cancellationToken);
    Task<int> CountAsync(LogQueryOptions options, CancellationToken cancellationToken);
    Task<IReadOnlyList<LogEntryRecord>> QueryAsync(LogQueryOptions options, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}

