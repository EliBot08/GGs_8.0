using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace GGs.E2ETests;

public class Prompt25Tests
{
    [Fact]
    public async Task Ingest_Persists_And_ReturnsAcceptedCount()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        var payload = new[] {
            new { eventId = Guid.NewGuid().ToString("N"), type = "test.event", payload = new { a = 1 }, createdUtc = DateTime.UtcNow },
            new { eventId = Guid.NewGuid().ToString("N"), type = "test.event", payload = new { b = 2 }, createdUtc = DateTime.UtcNow }
        };
        var resp = await client.PostAsJsonAsync("/api/ingest/events", payload);
        resp.EnsureSuccessStatusCode();
        var res = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(res.TryGetProperty("accepted", out var acc));
        Assert.Equal(2, acc.GetInt32());
    }

    [Fact]
    public async Task Ingest_TooLargeBody_Should413()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        // Create ~3MB payload
        var big = new string('x', 3 * 1024 * 1024);
        var payload = new[] { new { eventId = Guid.NewGuid().ToString("N"), type = "big", payload = new { big } } };
        var resp = await client.PostAsJsonAsync("/api/ingest/events", payload);
        Assert.True(resp.StatusCode == HttpStatusCode.RequestEntityTooLarge || (int)resp.StatusCode == 413);
    }

    [Fact]
    public async Task Ingest_RateLimit_429()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 40; i++)
        {
            var p = new[] { new { eventId = Guid.NewGuid().ToString("N"), type = "burst", payload = new { i } } };
            tasks.Add(client.PostAsJsonAsync("/api/ingest/events", p));
        }
        var results = await Task.WhenAll(tasks);
        // At least one should be 429 when crossing the 20 req/s limit
        Assert.Contains(results, r => r.StatusCode == (HttpStatusCode)429);
    }
}

