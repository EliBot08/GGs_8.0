using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt07Tests
{
    [Fact]
    public async Task HttpClient_AutoRefresh_On401_RetriesAndSucceeds()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        using var loginClient = factory.CreateClient();
        var login = await loginClient.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        using var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var access = loginDoc.RootElement.GetProperty("accessToken").GetString()!;
        var refresh = loginDoc.RootElement.GetProperty("refreshToken").GetString()!;

        var client = factory.CreateClient();
        var handler = new AutoRefreshHandler(factory.CreateClient(), refresh);
        var http = new HttpClient(handler) { BaseAddress = client.BaseAddress };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // First attempt with invalid token will yield 401; handler should refresh and retry
        var res = await http.GetAsync("/api/analytics/summary");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task AnalyticsSummary_Smoke_Admin()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var access = doc.RootElement.GetProperty("accessToken").GetString()!;

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var summary = await authed.GetAsync("/api/analytics/summary");
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
    }

    private sealed class AutoRefreshHandler : DelegatingHandler
    {
        private readonly HttpClient _authClient;
        private string _refreshToken;
        private string? _accessToken;

        public AutoRefreshHandler(HttpClient authClient, string refreshToken)
            : base(new HttpClientHandler())
        {
            _authClient = authClient;
            _refreshToken = refreshToken;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // First attempt
            var response = await base.SendAsync(Clone(request), cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            // Try refresh
            var refreshRes = await _authClient.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = _refreshToken, deviceId = Environment.MachineName }, cancellationToken);
            if (!refreshRes.IsSuccessStatusCode) return response; // give up
            using var doc = JsonDocument.Parse(await refreshRes.Content.ReadAsStringAsync(cancellationToken));
            _accessToken = doc.RootElement.GetProperty("accessToken").GetString();
            _refreshToken = doc.RootElement.GetProperty("refreshToken").GetString() ?? _refreshToken;

            // Retry original
            var retry = Clone(request);
            if (!string.IsNullOrWhiteSpace(_accessToken))
                retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            response.Dispose();
            return await base.SendAsync(retry, cancellationToken);
        }

        private static HttpRequestMessage Clone(HttpRequestMessage original)
        {
            var clone = new HttpRequestMessage(original.Method, original.RequestUri);
            // headers
            foreach (var h in original.Headers)
            {
                clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            if (original.Content != null)
            {
                var ms = new MemoryStream();
                original.Content.CopyToAsync(ms).GetAwaiter().GetResult();
                ms.Position = 0;
                var content = new StreamContent(ms);
                foreach (var h in original.Content.Headers)
                    content.Headers.Add(h.Key, h.Value);
                clone.Content = content;
            }
            return clone;
        }
    }
}

