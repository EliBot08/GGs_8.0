using System;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt27Tests
{
    [Fact]
    public void Production_MissingSecrets_ShouldFailStartup()
    {
        Assert.ThrowsAny<Exception>(() =>
        {
            using var factory = new TestAppFactory().WithWebHostBuilder(b => b.UseEnvironment("Production"));
            using var client = factory.CreateClient();
        });
    }

    [Fact]
    public void Production_WithEnvSecrets_ShouldStart()
    {
        using var factory = new TestAppFactory().WithWebHostBuilder(b => b
            .UseEnvironment("Production")
            .ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:JwtKey"] = "0123456789ABCDEF0123456789ABCDEF",
                    ["License:PublicKeyPem"] = "-----BEGIN PUBLIC KEY-----\nFAKE\n-----END PUBLIC KEY-----\n",
                    ["Server:AllowedOrigins"] = "http://localhost"
                });
            }));
        using var client = factory.CreateClient();
        var resp = client.GetAsync("/api/jwks").GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();
    }
}

