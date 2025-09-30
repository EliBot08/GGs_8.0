using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Xunit;

namespace GGs.NewE2ETests;

public class Prompt19Tests
{
    [Fact]
    public async Task Serilog_Request_Logging_Does_Not_Block_Requests()
    {
await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development")
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    var tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ggs_test_{Guid.NewGuid():N}.db");
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = $"Data Source={tempDb}",
                        ["Database:UseEnsureCreated"] = "true"
                    });
                }));
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/live");
        resp.EnsureSuccessStatusCode();
    }
}

