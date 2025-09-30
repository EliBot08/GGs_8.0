using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GGs.NewE2ETests;

public class Prompt18Tests
{
    [Fact]
    public async Task Server_Starts_With_Otel_Disabled_And_Enabled()
    {
await using (var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development")
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    var tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ggs_test_{Guid.NewGuid():N}.db");
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = $"Data Source={tempDb}",
                        ["Database:UseEnsureCreated"] = "true"
                    });
                })))
        {
            var client = factory.CreateClient();
            var resp = await client.GetAsync("/live");
            resp.EnsureSuccessStatusCode();
        }

await using (var factory2 = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development")
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    var tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ggs_test_{Guid.NewGuid():N}.db");
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = $"Data Source={tempDb}",
                        ["Database:UseEnsureCreated"] = "true",
                        ["Otel:ServerEnabled"] = "true",
                        ["Otel:OtlpEndpoint"] = "http://localhost:4317"
                    });
                })))
        {
            var client2 = factory2.CreateClient();
            var resp2 = await client2.GetAsync("/live");
            resp2.EnsureSuccessStatusCode();
        }
    }
}

