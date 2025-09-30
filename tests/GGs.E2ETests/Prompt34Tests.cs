using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace GGs.E2ETests;

public class Prompt34Tests
{
    [Fact]
    public async Task Archival_MovesOldAuditLogs_AndRemovesFromDb()
    {
        await using var factory = new TestAppFactory().WithWebHostBuilder(b => b.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Retention:Enabled"] = "true",
                ["Retention:RunOnStartup"] = "false" // we'll trigger manually
            });
        }));
        var client = factory.CreateClient();

        // Login as admin
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = json.GetProperty("accessToken").GetString()!;

        // Seed a couple of old logs (> 120 days)
        var old1 = new { deviceId = "dev-arch1", tweakId = Guid.NewGuid(), success = true, appliedUtc = DateTime.UtcNow.AddDays(-150) };
        var old2 = new { deviceId = "dev-arch2", tweakId = Guid.NewGuid(), success = false, appliedUtc = DateTime.UtcNow.AddDays(-200) };
        var post1 = new HttpRequestMessage(HttpMethod.Post, "/api/audit/log") { Content = JsonContent.Create(old1) };
        post1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp1 = await client.SendAsync(post1); resp1.EnsureSuccessStatusCode();
        var post2 = new HttpRequestMessage(HttpMethod.Post, "/api/audit/log") { Content = JsonContent.Create(old2) };
        post2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp2 = await client.SendAsync(post2); resp2.EnsureSuccessStatusCode();

        // Trigger archival
        var run = new HttpRequestMessage(HttpMethod.Post, "/api/admin/run-archival");
        run.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var runResp = await client.SendAsync(run);
        runResp.EnsureSuccessStatusCode();
        var runJson = await runResp.Content.ReadFromJsonAsync<JsonElement>();
        var archived = runJson.GetProperty("archived").GetInt32();
        Assert.True(archived >= 2);

        // Query search to ensure old ones are gone
        var searchReq = new HttpRequestMessage(HttpMethod.Get, $"/api/audit/search?from={Uri.EscapeDataString(DateTime.UtcNow.AddYears(-5).ToString("o"))}&to={Uri.EscapeDataString(DateTime.UtcNow.AddYears(-1).ToString("o"))}");
        searchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var searchResp = await client.SendAsync(searchReq);
        searchResp.EnsureSuccessStatusCode();
        var arr = await searchResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(arr.GetArrayLength() == 0);
    }
}

