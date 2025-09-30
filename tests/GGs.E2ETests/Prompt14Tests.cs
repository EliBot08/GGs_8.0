using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt14Tests
{
    [Fact]
    public async Task Invalid_Login_Returns_ProblemDetails_400()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "", password = "" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("application/problem+json", resp.Content.Headers.ContentType!.MediaType + "/" + resp.Content.Headers.ContentType!.MediaSubtype);
    }

    [Fact]
    public async Task Invalid_Tweak_Create_Returns_ProblemDetails_400()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var client = factory.CreateClient();

        // Login as admin
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("accessToken").GetString();
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Missing Name should fail
        var resp = await authed.PostAsJsonAsync("/api/v1/tweaks", new { Description = "test" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.StartsWith("application/problem+json", resp.Content.Headers.ContentType!.ToString());
    }

    [Fact]
    public async Task License_Issue_Invalid_ExpiresUtc_Returns_ProblemDetails_400()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var client = factory.CreateClient();

        // Login as admin
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("accessToken").GetString();
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create a user
        var u = await authed.PostAsJsonAsync("/api/v1/users", new { Email = "valtest@example.com", Password = "P@ssw0rd!", Roles = new[] { "User" } });
        u.EnsureSuccessStatusCode();
        var udoc = JsonDocument.Parse(await u.Content.ReadAsStringAsync());
        var userId = udoc.RootElement.GetProperty("id").GetString();

        var past = DateTime.UtcNow.AddDays(-1);
        var body = new { UserId = userId, Tier = "Pro", ExpiresUtc = past, IsAdminKey = false };
        var resp = await authed.PostAsJsonAsync("/api/v1/licenses/issue", body);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.StartsWith("application/problem+json", resp.Content.Headers.ContentType!.ToString());
    }
}

