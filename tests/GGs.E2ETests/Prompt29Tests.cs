using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GGs.E2ETests;

public class Prompt29Tests
{
    private async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { username = email, password });
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Policies_Enforced_ByRoleMappings()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        // Login as seeded admin
        var adminToken = await LoginAsync(client, "admin@ggs.local", "ChangeMe!123");

        // Create manager and support users via Users API
        var adminReq = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email = "manager@ggs.local", password = "Pass!12345", roles = new[] { "Manager" } })
        };
        adminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var createMgr = await client.SendAsync(adminReq);
        createMgr.EnsureSuccessStatusCode();

        adminReq = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email = "support@ggs.local", password = "Pass!12345", roles = new[] { "Support" } })
        };
        adminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var createSup = await client.SendAsync(adminReq);
        createSup.EnsureSuccessStatusCode();

        var mgrToken = await LoginAsync(client, "manager@ggs.local", "Pass!12345");
        var supToken = await LoginAsync(client, "support@ggs.local", "Pass!12345");

        // Support can view analytics
        var supReq = new HttpRequestMessage(HttpMethod.Get, "/api/analytics/summary");
        supReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supToken);
        var supAnalytics = await client.SendAsync(supReq);
        Assert.True(supAnalytics.IsSuccessStatusCode);

        // Support cannot list users (ManageUsers)
        var supUsers = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        supUsers.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supToken);
        var respUsers = await client.SendAsync(supUsers);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, respUsers.StatusCode);

        // Manager can access remote connections (ExecuteRemote)
        var mgrRemote = new HttpRequestMessage(HttpMethod.Get, "/api/remote/connections");
        mgrRemote.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
        var respRemote = await client.SendAsync(mgrRemote);
        Assert.True(respRemote.IsSuccessStatusCode);

        // Manager can access licenses list (ManageLicenses -> default Admin,Manager)
        var mgrLic = new HttpRequestMessage(HttpMethod.Get, "/api/licenses");
        mgrLic.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
        var respLic = await client.SendAsync(mgrLic);
        Assert.True(respLic.IsSuccessStatusCode);
    }
}

