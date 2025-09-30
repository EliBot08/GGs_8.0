using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt19Tests
{
    [Fact]
    public async Task Serilog_Request_Logging_Does_Not_Block_Requests()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/live");
        resp.EnsureSuccessStatusCode();
    }
}

