using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Xunit;

namespace GGs.NewE2ETests;

public class Prompt13Tests
{
    [Fact]
    public async Task V1_Routes_Work_And_Legacy_Routes_Emit_Deprecation_Headers()
    {
await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Staging")
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    var tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ggs_test_{Guid.NewGuid():N}.db");
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = $"Data Source={tempDb}",
                        ["Database:UseEnsureCreated"] = "false",
                        ["Auth:JwtKey"] = "0123456789ABCDEF0123456789ABCDEF"
                    });
                }));

        using var client = factory.CreateClient();

        var v1Login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        v1Login.EnsureSuccessStatusCode();

        var legacyLogin = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        legacyLogin.EnsureSuccessStatusCode();
        Assert.True(legacyLogin.Headers.TryGetValues("Deprecation", out var depValues) && depValues.FirstOrDefault() == "true");
        Assert.True(legacyLogin.Headers.Contains("Sunset"));
    }
}

