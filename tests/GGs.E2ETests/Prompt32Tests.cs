using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace GGs.E2ETests;

public class Prompt32Tests
{
    [Fact]
    public async Task Oversized_Request_ShouldReturn_413()
    {
        await using var factory = new TestAppFactory().WithWebHostBuilder(b => b.ConfigureAppConfiguration((ctx, cfg) =>
        {
            // Ensure default max body size (~2MB) is in effect
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Kestrel:MaxRequestBodySizeBytes"] = "2000000"
            });
        }));
        var client = factory.CreateClient();

        // Create a ~3 MB JSON body to exceed the limit
        var big = new string('x', 3 * 1024 * 1024);
        var json = $"{{\"x\":\"{big}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = content };
        var resp = await client.SendAsync(req);
        Assert.True(resp.StatusCode == HttpStatusCode.RequestEntityTooLarge || (int)resp.StatusCode == 413);
    }
}

