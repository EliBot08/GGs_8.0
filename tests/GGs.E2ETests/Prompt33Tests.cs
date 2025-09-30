using System;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt33Tests
{
    [Fact]
    public void Production_WithPendingMigrations_ShouldFailStartup()
    {
        // Fresh test DB will have pending migrations until applied; prod must fail fast
        Assert.ThrowsAny<Exception>(() =>
        {
            using var factory = new TestAppFactory().WithWebHostBuilder(b => b.UseEnvironment("Production"));
            using var client = factory.CreateClient();
        });
    }

    [Fact]
    public async Task Admin_Migrations_Endpoint_Returns_Status()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        // Login as seeded admin
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var json = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var token = json.GetProperty("accessToken").GetString()!;
        var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "/api/admin/migrations");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("applied", body);
        Assert.Contains("pending", body);
    }
}

