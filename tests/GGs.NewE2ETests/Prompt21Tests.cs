using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GGs.Server.Data;
using GGs.Shared.Tweaks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GGs.NewE2ETests;

public class Prompt21Tests
{
    [Fact]
    public async Task Audit_Search_Returns_Filtered_Subsets()
    {
        await using var factory = new TestAppFactory();

        // Seed some audit logs
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            db.TweakLogs.AddRange(
                new TweakApplicationLog { Id = Guid.NewGuid(), TweakId = Guid.NewGuid(), TweakName = "Alpha", DeviceId = "DEV-A", UserId = "U1", AppliedUtc = now.AddMinutes(-30), Success = true },
                new TweakApplicationLog { Id = Guid.NewGuid(), TweakId = Guid.NewGuid(), TweakName = "Beta", DeviceId = "DEV-B", UserId = "U2", AppliedUtc = now.AddHours(-2), Success = false },
                new TweakApplicationLog { Id = Guid.NewGuid(), TweakId = Guid.NewGuid(), TweakName = "Gamma", DeviceId = "DEV-A", UserId = "U1", AppliedUtc = now.AddMinutes(-5), Success = true }
            );
            db.SaveChanges();
        }

        // Login as admin
        var loginClient = factory.CreateClient();
        var login = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginDoc.RootElement.GetProperty("accessToken").GetString();

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Search recent for DEV-A success
        var qs = "?deviceId=DEV-A&success=true&from=" + Uri.EscapeDataString(DateTime.UtcNow.AddHours(-1).ToString("o"));
        var res = await authed.GetAsync("/api/v1/audit/search" + qs);
        res.EnsureSuccessStatusCode();
        var items = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.EnumerateArray().ToList();
        Assert.True(items.Count >= 1);
        Assert.All(items, e => Assert.Equal("DEV-A", e.GetProperty("deviceId").GetString()));
        Assert.All(items, e => Assert.True(e.GetProperty("success").GetBoolean()));
    }
}
