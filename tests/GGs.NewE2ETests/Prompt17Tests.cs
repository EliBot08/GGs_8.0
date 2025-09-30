using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;
using System.Collections.Generic;

namespace GGs.NewE2ETests;

public class Prompt17Tests
{
    [Fact]
    public async Task SecurityHeaders_Present_In_Production()
    {
await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Staging")
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = $"Data Source={System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ggs_test_{Guid.NewGuid():N}.db")}",
                        ["Database:UseEnsureCreated"] = "false",
                        ["License:PublicKeyPem"] = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAtestkeyforci\n-----END PUBLIC KEY-----"
                    });
                }));

        var client = factory.CreateClient();
        var resp = await client.GetAsync("/live");
        resp.EnsureSuccessStatusCode();
        Assert.True(resp.Headers.Contains("X-Frame-Options") || resp.Headers.Contains("x-frame-options"));
        Assert.True(resp.Headers.Contains("X-Content-Type-Options") || resp.Headers.Contains("x-content-type-options"));
    }
}

