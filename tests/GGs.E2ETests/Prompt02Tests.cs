using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using Xunit;

namespace GGs.E2ETests;

public class Prompt02Tests
{
    private static async Task<string> LoginAsync(WebApplicationFactory<GGs.Server.Program> factory)
    {
        using var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static HttpClient CreateAuthedClient(WebApplicationFactory<GGs.Server.Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Enroll_List_Revoke_PagingAndTotals_Work()
    {
        await using var factory = new TestAppFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var token = await LoginAsync(factory);
        using var client = CreateAuthedClient(factory, token);

        // Enroll two devices
        var enrollA = await client.PostAsJsonAsync("/api/devices/enroll", new { deviceId = "device-a", thumbprint = "THUMB-A", commonName = "A" });
        Assert.Equal(HttpStatusCode.OK, enrollA.StatusCode);
        var enrollB = await client.PostAsJsonAsync("/api/devices/enroll", new { deviceId = "device-b", thumbprint = "THUMB-B", commonName = "B" });
        Assert.Equal(HttpStatusCode.OK, enrollB.StatusCode);

        // Page size 1, expect total=2
        var page1 = await client.GetAsync("/api/devices?skip=0&take=1");
        Assert.Equal(HttpStatusCode.OK, page1.StatusCode);
        Assert.True(page1.Headers.TryGetValues("X-Total-Count", out var totals1));
        Assert.Equal("2", totals1.Single());
        var items1 = await page1.Content.ReadFromJsonAsync<List<DeviceRec>>();
        Assert.NotNull(items1);
        Assert.Equal(1, items1!.Count);

        // Filter by q
        var qA = await client.GetAsync("/api/devices?skip=0&take=10&q=device-a");
        Assert.Equal(HttpStatusCode.OK, qA.StatusCode);
        Assert.True(qA.Headers.TryGetValues("X-Total-Count", out var totalsA));
        Assert.Equal("1", totalsA.Single());
        var listA = await qA.Content.ReadFromJsonAsync<List<DeviceRec>>();
        Assert.NotNull(listA);
        Assert.Single(listA!);
        Assert.True(listA![0].IsActive);

        // Revoke A
        var revoke = await client.PostAsync("/api/devices/device-a/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        // Active filter should exclude A now
        var activeA = await client.GetAsync("/api/devices?isActive=true&q=device-a");
        Assert.True(activeA.Headers.TryGetValues("X-Total-Count", out var totalsActiveA));
        Assert.Equal("0", totalsActiveA.Single());
        var activeListA = await activeA.Content.ReadFromJsonAsync<List<DeviceRec>>();
        Assert.NotNull(activeListA);
        Assert.Empty(activeListA!);

        // Inactive filter should include A
        var inactiveA = await client.GetAsync("/api/devices?isActive=false&q=device-a");
        Assert.True(inactiveA.Headers.TryGetValues("X-Total-Count", out var totalsInactiveA));
        Assert.Equal("1", totalsInactiveA.Single());
        var inactiveListA = await inactiveA.Content.ReadFromJsonAsync<List<DeviceRec>>();
        Assert.NotNull(inactiveListA);
        Assert.Single(inactiveListA!);
        Assert.False(inactiveListA![0].IsActive);
    }

    private sealed class DeviceRec
    {
        public int Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Thumbprint { get; set; } = string.Empty;
        public string? CommonName { get; set; }
        public DateTime RegisteredUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public DateTime? RevokedUtc { get; set; }
        public bool IsActive { get; set; }
    }
}

