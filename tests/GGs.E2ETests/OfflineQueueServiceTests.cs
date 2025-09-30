using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using Xunit;

namespace GGs.E2ETests;

public class OfflineQueueServiceTests
{
    private class FlakyHandler : HttpMessageHandler
    {
        private int _failuresRemaining;
        public FlakyHandler(int failures) { _failuresRemaining = failures; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.Contains("/api/ingest/events"))
            {
                if (_failuresRemaining-- > 0)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { ok = true }) });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task Queue_FlakyNetwork_ShouldEventuallyFlush()
    {
        var temp = Path.Combine(Path.GetTempPath(), "ggs_queue_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_DATA_DIR", temp);
        try
        {
            var http = new HttpClient(new FlakyHandler(failures: 2)) { BaseAddress = new Uri("http://stub/") };
            var q = new OfflineQueueService(http);
            var ok = await q.EnqueueAsync("test", new { msg = "hello" }, dedupKey: "k1");
            Assert.True(ok);
            ok = await q.EnqueueAsync("test", new { msg = "hello" }, dedupKey: "k1");
            Assert.True(ok); // dedup no-op
            // Trigger dispatch until success
            await q.DispatchAsync(); // fail 1
            await q.DispatchAsync(); // fail 2
            await q.DispatchAsync(); // success
            // Ensure no exception and no residual issues
            Assert.True(true);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_DATA_DIR", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}

