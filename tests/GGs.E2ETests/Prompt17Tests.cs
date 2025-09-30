using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using System.Collections.Generic;

namespace GGs.E2ETests;

public class Prompt17Tests
{
    [Fact]
    public async Task SecurityHeaders_Present_In_Production()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Production")
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Auth:JwtKey"] = "super-secret-key-1234567890",
                        ["License:PublicKeyPem"] = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAn\n-----END PUBLIC KEY-----",
                        ["Server:AllowedOrigins"] = "http://localhost"
                    });
                }));

        var client = factory.CreateClient();
        var resp = await client.GetAsync("/live");
        resp.EnsureSuccessStatusCode();
        Assert.True(resp.Headers.Contains("Strict-Transport-Security") || resp.Headers.Contains("strict-transport-security"));
        Assert.True(resp.Headers.Contains("X-Frame-Options") || resp.Headers.Contains("x-frame-options"));
        Assert.True(resp.Headers.Contains("X-Content-Type-Options") || resp.Headers.Contains("x-content-type-options"));
    }
}
