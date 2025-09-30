using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt01Tests
{
    [Fact]
    public async Task DevEnvironment_LiveAndReady_Return200()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        using var client = factory.CreateClient();

        var live = await client.GetAsync("/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        var ready = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task ProdEnvironment_MissingSecrets_ShouldFailStartup()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var factory = new WebApplicationFactory<GGs.Server.Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

            using var client = factory.CreateClient();
            _ = await client.GetAsync("/live");
        });
    }
}

