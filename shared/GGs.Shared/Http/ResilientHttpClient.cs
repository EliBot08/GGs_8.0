using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GGs.Shared.Http;

public sealed class ResilientHttpClient
{
    private readonly HttpClient _http;
    private readonly int _maxAttempts;
    private readonly int _baseDelayMs;

    public ResilientHttpClient(HttpClient http, int maxAttempts = 3, int baseDelayMs = 250)
    {
        _http = http;
        _maxAttempts = Math.Max(1, maxAttempts);
        _baseDelayMs = Math.Max(0, baseDelayMs);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        Exception? last = null;
        for (int attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            var reqToSend = attempt == 1 ? request : CloneRequest(request);
            try
            {
                return await _http.SendAsync(reqToSend, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw; // do not retry cancellations/timeouts
            }
            catch (Exception ex)
            {
                last = ex;
                if (attempt >= _maxAttempts) break;
                var delay = ComputeDelay(attempt);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
        throw last!;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        // copy headers
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        // copy content if present
        if (request.Content != null)
        {
            // Buffer original content into memory
            var ms = new System.IO.MemoryStream();
            request.Content.CopyTo(ms, null, System.Threading.CancellationToken.None);
            ms.Position = 0;
            var newContent = new StreamContent(ms);
            foreach (var h in request.Content.Headers)
                newContent.Headers.TryAddWithoutValidation(h.Key, h.Value);
            clone.Content = newContent;
        }
        // version/options
        clone.Version = request.Version;
        clone.VersionPolicy = request.VersionPolicy;
        foreach (var prop in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(prop.Key), prop.Value);
        }
        return clone;
    }

    private TimeSpan ComputeDelay(int attempt)
    {
        // Quadratic backoff with jitter
        var baseMs = Math.Min(10_000, attempt * attempt * _baseDelayMs);
        var jitter = Random.Shared.Next(-baseMs / 3, baseMs / 3 + 1);
        return TimeSpan.FromMilliseconds(Math.Max(50, baseMs + jitter));
    }
}

public static class ResilientHttpClientExtensions
{
    public static ResilientHttpClient WithResilience(this HttpClient http, int maxAttempts = 3, int baseDelayMs = 250)
        => new(http, maxAttempts, baseDelayMs);
}

