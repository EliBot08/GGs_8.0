using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt18Tests
{
    [Fact]
    public async Task Server_Starts_With_Otel_Disabled_And_Enabled()
    {
        // Disabled (default)
        await using (var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development")))
        {
            var client = factory.CreateClient();
            var resp = await client.GetAsync("/live");
            resp.EnsureSuccessStatusCode();
        }

        // Enabled via config
        await using (var factory2 = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development")
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
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

