using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt10Tests
{
    [Fact]
    public async Task License_Lifecycle_Update_Enforcement()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        // login admin
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var lr = await login.Content.ReadFromJsonAsync<LoginResponse>();
        var token = lr!.accessToken;

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create a user
        var createUser = await authed.PostAsJsonAsync("/api/users", new { Email = "lic-user@example.com", Password = "P@ssw0rd!", Roles = new[] { "User" } });
        createUser.EnsureSuccessStatusCode();
        var userDoc = JsonDocument.Parse(await createUser.Content.ReadAsStringAsync());
        var userId = userDoc.RootElement.GetProperty("id").GetString();

        // Issue a license
        var issue = await authed.PostAsJsonAsync("/api/licenses/issue", new { UserId = userId, Tier = "Pro", AllowOfflineValidation = true });
        issue.EnsureSuccessStatusCode();
        var licJson = JsonDocument.Parse(await issue.Content.ReadAsStringAsync());
        var signed = licJson.RootElement.GetProperty("license");
        var licenseId = signed.GetProperty("payload").GetProperty("licenseId").GetString();

        // Set MaxDevices=1
        var upd = await authed.PostAsJsonAsync($"/api/licenses/update/{licenseId}", new { MaxDevices = 1, DeveloperMode = false, Notes = "test" });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, upd.StatusCode);

        // Validate from device A -> ok
        var reqA = new { License = signed.Deserialize<JsonElement>(), CurrentDeviceBinding = "DEV-A" };
        var valA = await authed.PostAsJsonAsync("/api/licenses/validate", reqA);
        valA.EnsureSuccessStatusCode();
        var valAj = await valA.Content.ReadFromJsonAsync<ValidateResponse>();
        Assert.True(valAj!.IsValid);

        // Validate from device B -> should fail due to MaxDevices=1
        var reqB = new { License = signed.Deserialize<JsonElement>(), CurrentDeviceBinding = "DEV-B" };
        var valB = await authed.PostAsJsonAsync("/api/licenses/validate", reqB);
        valB.EnsureSuccessStatusCode();
        var valBj = await valB.Content.ReadFromJsonAsync<ValidateResponse>();
        Assert.False(valBj!.IsValid);

        // Suspend and validate again from device A -> should be invalid
        var susp = await authed.PostAsync($"/api/licenses/suspend/{licenseId}", null);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, susp.StatusCode);
        var valA2 = await authed.PostAsJsonAsync("/api/licenses/validate", reqA);
        valA2.EnsureSuccessStatusCode();
        var valA2j = await valA2.Content.ReadFromJsonAsync<ValidateResponse>();
        Assert.False(valA2j!.IsValid);

        // Activate and validate from A -> should be valid again
        var act = await authed.PostAsync($"/api/licenses/activate/{licenseId}", null);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, act.StatusCode);
        var valA3 = await authed.PostAsJsonAsync("/api/licenses/validate", reqA);
        valA3.EnsureSuccessStatusCode();
        var valA3j = await valA3.Content.ReadFromJsonAsync<ValidateResponse>();
        Assert.True(valA3j!.IsValid);
    }

    private sealed class LoginResponse { public string accessToken { get; set; } = string.Empty; }
    private sealed class ValidateResponse { public bool IsValid { get; set; } public string? Message { get; set; } }
}

