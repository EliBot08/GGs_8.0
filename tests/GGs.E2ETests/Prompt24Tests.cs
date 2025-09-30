using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt24Tests
{
    [Fact]
    public async Task Ingest_NoCert_Should401_WhenEnabled()
    {
        await using var factory = new TestAppFactory()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:ClientCertificate:Enabled"] = "true"
                });
            }));

        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/ingest/events", new object[] { });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithDebugHeader_Should200_WhenEnabled()
    {
        await using var factory = new TestAppFactory()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:ClientCertificate:Enabled"] = "true"
                });
            }));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-ClientCert", "1");
        var resp = await client.PostAsJsonAsync("/api/ingest/events", new object[] { });
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Audit_NoCert_Should401_WhenEnabled()
    {
        await using var factory = new TestAppFactory()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:ClientCertificate:Enabled"] = "true"
                });
            }));

        var client = factory.CreateClient();
        var body = new { deviceId = "dev1", tweakId = Guid.Empty, success = true };
        var resp = await client.PostAsJsonAsync("/api/audit/log", body);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Audit_WithDebugHeader_Should200_WhenEnabled()
    {
        await using var factory = new TestAppFactory()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:ClientCertificate:Enabled"] = "true"
                });
            }));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-ClientCert", "1");
        var body = new { deviceId = "dev1", tweakId = Guid.Empty, success = true };
        var resp = await client.PostAsJsonAsync("/api/audit/log", body);
        resp.EnsureSuccessStatusCode();
    }
}

