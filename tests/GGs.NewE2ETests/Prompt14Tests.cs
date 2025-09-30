using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Xunit;

namespace GGs.NewE2ETests;

public class Prompt14Tests
{
    [Fact]
    public async Task Invalid_Payloads_Return_ProblemDetails()
    {
await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Staging")
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    var tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ggs_test_{Guid.NewGuid():N}.db");
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = $"Data Source={tempDb}",
                        ["Database:UseEnsureCreated"] = "false",
                        ["Auth:JwtKey"] = "0123456789ABCDEF0123456789ABCDEF"
                    });
                }));
        var client = factory.CreateClient();

        var resp1 = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "", password = "" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp1.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var access = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);
        var resp2 = await authed.PostAsJsonAsync("/api/v1/tweaks", new { Description = "test" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp2.StatusCode);
    }
}

