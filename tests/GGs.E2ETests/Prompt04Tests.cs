using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using GGs.Shared.Tweaks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace GGs.E2ETests;

public class Prompt04Tests
{
    private static WebApplicationFactory<GGs.Server.Program> CreateFactoryWithToken(string token)
        => new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development")
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Audit:MachineToken"] = token
                    });
                }));

    [Fact]
    public async Task NewAudit_WithMachineToken_Returns200()
    {
        const string token = "test-token-123";
        await using var factory = CreateFactoryWithToken(token);
        using var client = factory.CreateClient();
        var log = new TweakApplicationLog
        {
            Id = Guid.NewGuid(),
            TweakId = Guid.NewGuid(),
            TweakName = "e2e",
            DeviceId = "device-e2e",
            AppliedUtc = DateTime.UtcNow,
            Success = true,
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/audit/log")
        {
            Content = JsonContent.Create(log)
        };
        req.Headers.TryAddWithoutValidation("X-Machine-Token", token);
        req.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString());
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task NewAudit_WithoutToken_NoAuthOrCert_Returns401()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        var log = new TweakApplicationLog
        {
            Id = Guid.NewGuid(),
            TweakId = Guid.NewGuid(),
            DeviceId = "device-e2e",
            AppliedUtc = DateTime.UtcNow,
            Success = true,
        };
        var resp = await client.PostAsJsonAsync("/api/audit/log", log);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task LegacyAudit_WithMachineToken_Returns200()
    {
        const string token = "legacy-token-xyz";
        await using var factory = CreateFactoryWithToken(token);
        using var client = factory.CreateClient();
        var log = new TweakApplicationLog
        {
            Id = Guid.NewGuid(),
            TweakId = Guid.NewGuid(),
            TweakName = "e2e",
            DeviceId = "device-e2e",
            AppliedUtc = DateTime.UtcNow,
            Success = true,
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auditlogs")
        {
            Content = JsonContent.Create(log)
        };
        req.Headers.TryAddWithoutValidation("X-Machine-Token", token);
        req.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString());
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task LegacyAudit_WithoutToken_NoAuthOrCert_Returns401()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        var log = new TweakApplicationLog
        {
            Id = Guid.NewGuid(),
            TweakId = Guid.NewGuid(),
            DeviceId = "device-e2e",
            AppliedUtc = DateTime.UtcNow,
            Success = true,
        };
        var resp = await client.PostAsJsonAsync("/api/auditlogs", log);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}

