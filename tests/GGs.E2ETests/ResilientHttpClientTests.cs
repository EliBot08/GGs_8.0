using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GGs.Shared.Http;
using Xunit;

namespace GGs.E2ETests;

public class ResilientHttpClientTests
{
    private sealed class FlakyHandler : DelegatingHandler
    {
        private int _failuresRemaining;
        public FlakyHandler(int failures) { _failuresRemaining = failures; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Decrement(ref _failuresRemaining) >= 0)
            {
                throw new HttpRequestException("Simulated failure");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        }
    }

    [Fact]
    public async Task ResilientClient_Retries_On_Failures()
    {
        var http = new HttpClient(new FlakyHandler(failures: 2)) { BaseAddress = new Uri("http://stub/") };
        var resilient = http.WithResilience(maxAttempts: 3, baseDelayMs: 10);
        using var req = new HttpRequestMessage(HttpMethod.Get, "/");
        var resp = await resilient.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Theory]
    [InlineData("aa bb:cc", "AABBCC")]
    [InlineData("AA:BB:CC", "AABBCC")]
    [InlineData("aabbcc", "AABBCC")]
    public void NormalizeThumbprint_Works(string input, string expected)
    {
        var norm = SecureHttpClientFactory.NormalizeThumbprint(input);
        Assert.Equal(expected, norm);
    }
}

