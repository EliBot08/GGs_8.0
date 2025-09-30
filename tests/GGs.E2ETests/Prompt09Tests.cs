using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt09Tests
{
    [Fact]
    public async Task ImportCsv_CreateUsers_AndChangeRoles_SendWelcome()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var lr = await login.Content.ReadFromJsonAsync<LoginResponse>();
        var token = lr!.accessToken;

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Prepare CSV content: one valid, one invalid (missing password)
        var csvText = "Email,Password,Roles\nuser1@example.com,P@ssw0rd,User|Support\nuser2@example.com,,User";
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvText));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        multipart.Add(fileContent, "file", "users.csv");

        var importRes = await authed.PostAsync("/api/users/import", multipart);
        importRes.EnsureSuccessStatusCode();
        using var importDoc = JsonDocument.Parse(await importRes.Content.ReadAsStringAsync());
        var created = importDoc.RootElement.GetProperty("created");
        var errors = importDoc.RootElement.GetProperty("errors");
        Assert.True(created.GetArrayLength() >= 1);
        Assert.True(errors.GetArrayLength() >= 1);

        // Fetch users and find the imported one
        var usersRes = await authed.GetAsync("/api/users");
        usersRes.EnsureSuccessStatusCode();
        var usersJson = JsonDocument.Parse(await usersRes.Content.ReadAsStringAsync());
        var imported = usersJson.RootElement.EnumerateArray().First(e => e.GetProperty("email").GetString() == "user1@example.com");
        var userId = imported.GetProperty("id").GetString();

        // Change roles to Support only
        var setRoles = await authed.PostAsJsonAsync($"/api/users/{userId}/roles", new { roles = new[] { "Support" } });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, setRoles.StatusCode);

        // Verify change
        var usersRes2 = await authed.GetAsync("/api/users");
        usersRes2.EnsureSuccessStatusCode();
        var usersJson2 = JsonDocument.Parse(await usersRes2.Content.ReadAsStringAsync());
        var updated = usersJson2.RootElement.EnumerateArray().First(e => e.GetProperty("email").GetString() == "user1@example.com");
        var rolesArr = updated.GetProperty("roles");
        Assert.Contains(rolesArr.EnumerateArray(), r => r.GetString() == "Support");

        // Welcome email
        var welcome = await authed.PostAsync($"/api/users/{userId}/welcome-email", null);
        Assert.Equal(System.Net.HttpStatusCode.Accepted, welcome.StatusCode);
    }

    private sealed class LoginResponse { public string accessToken { get; set; } = string.Empty; }
}
