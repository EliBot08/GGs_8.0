using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt13Tests
{
    [Fact]
    public async Task V1_Routes_Work_And_Legacy_Routes_Emit_Deprecation_Headers()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        using var client = factory.CreateClient();

        // v1 login works
        var v1Login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        v1Login.EnsureSuccessStatusCode();

        // legacy login still works and emits deprecation headers
        var legacyLogin = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        legacyLogin.EnsureSuccessStatusCode();
        Assert.True(legacyLogin.Headers.TryGetValues("Deprecation", out var depValues) && depValues.FirstOrDefault() == "true");
        Assert.True(legacyLogin.Headers.Contains("Sunset"));
    }
}

