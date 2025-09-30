using System.Net.Http.Headers;
using Xunit;

namespace GGs.E2ETests;

public class Prompt38Tests
{
    [Fact]
    public async Task CorrelationId_Header_Present_In_Response()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/live");
        resp.EnsureSuccessStatusCode();
        Assert.True(resp.Headers.Contains("X-Correlation-ID"));
    }
}

