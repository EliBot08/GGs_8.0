using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt08Tests
{
    [Fact]
    public async Task Roles_AdminGetsList_WithUserCounts()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var doc = await login.Content.ReadFromJsonAsync<LoginResponse>();
        var token = doc!.accessToken;

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await authed.GetAsync("/api/roles");
        res.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.True(json.RootElement.GetArrayLength() >= 4); // Admin, Manager, Support, User
        var hasAdmin = json.RootElement.EnumerateArray().Any(e => e.GetProperty("name").GetString() == "Admin");
        Assert.True(hasAdmin);
        var adminCount = json.RootElement.EnumerateArray().First(e => e.GetProperty("name").GetString() == "Admin").GetProperty("userCount").GetInt32();
        Assert.True(adminCount >= 1);
    }

    [Fact]
    public async Task Roles_Unauthenticated_Unauthorized()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        using var client = factory.CreateClient();
        var res = await client.GetAsync("/api/roles");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private sealed class LoginResponse { public string accessToken { get; set; } = string.Empty; }
}

