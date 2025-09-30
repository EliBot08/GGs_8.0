using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GGs.Server.Data;
using GGs.Server.Models;
using GGs.Shared.Tweaks;
using Xunit;

namespace GGs.NewE2ETests;

public class Prompt20Tests
{
    [Fact]
    public async Task Analytics_Enrichment_Endpoints_Work()
    {
        await using var factory = new TestAppFactory();

        // Seed domain data directly via DI
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Licenses: Basic x2 (Active), Pro x1 (Active), Pro x1 (Suspended)
            db.Licenses.AddRange(
                new LicenseRecord { Id = Guid.NewGuid(), LicenseId = "L1", UserId = "U1", Tier = "Basic", Status = "Active", IssuedUtc = DateTime.UtcNow, SignedLicenseJson = "{}", AllowOfflineValidation = true },
                new LicenseRecord { Id = Guid.NewGuid(), LicenseId = "L2", UserId = "U2", Tier = "Basic", Status = "Active", IssuedUtc = DateTime.UtcNow, SignedLicenseJson = "{}", AllowOfflineValidation = true },
                new LicenseRecord { Id = Guid.NewGuid(), LicenseId = "L3", UserId = "U3", Tier = "Pro", Status = "Active", IssuedUtc = DateTime.UtcNow, SignedLicenseJson = "{}", AllowOfflineValidation = true },
                new LicenseRecord { Id = Guid.NewGuid(), LicenseId = "L4", UserId = "U4", Tier = "Pro", Status = "Suspended", IssuedUtc = DateTime.UtcNow, SignedLicenseJson = "{}", AllowOfflineValidation = true }
            );
            // Tweak failure logs: Tweak A -> 3 fails, Tweak B -> 1 fail
            var tweakA = Guid.NewGuid();
            var tweakB = Guid.NewGuid();
            db.TweakLogs.AddRange(
                new TweakApplicationLog { Id = Guid.NewGuid(), TweakId = tweakA, TweakName = "Tweak A", DeviceId = "D1", AppliedUtc = DateTime.UtcNow.AddMinutes(-10), Success = false },
                new TweakApplicationLog { Id = Guid.NewGuid(), TweakId = tweakA, TweakName = "Tweak A", DeviceId = "D2", AppliedUtc = DateTime.UtcNow.AddMinutes(-9), Success = false },
                new TweakApplicationLog { Id = Guid.NewGuid(), TweakId = tweakA, TweakName = "Tweak A", DeviceId = "D3", AppliedUtc = DateTime.UtcNow.AddMinutes(-8), Success = false },
                new TweakApplicationLog { Id = Guid.NewGuid(), TweakId = tweakB, TweakName = "Tweak B", DeviceId = "D4", AppliedUtc = DateTime.UtcNow.AddMinutes(-7), Success = false }
            );
            // Device registrations
            db.DeviceRegistrations.AddRange(
                new DeviceRegistration { DeviceId = "DEV-1", Thumbprint = "T1", RegisteredUtc = DateTime.UtcNow.AddDays(-1), LastSeenUtc = DateTime.UtcNow.AddMinutes(-5), IsActive = true },
                new DeviceRegistration { DeviceId = "DEV-2", Thumbprint = "T2", RegisteredUtc = DateTime.UtcNow.AddDays(-1), LastSeenUtc = DateTime.UtcNow.AddMinutes(-120), IsActive = true },
                new DeviceRegistration { DeviceId = "DEV-3", Thumbprint = "T3", RegisteredUtc = DateTime.UtcNow.AddDays(-1), LastSeenUtc = DateTime.UtcNow.AddMinutes(-2), IsActive = false }
            );
            db.SaveChanges();
        }

        // Login to get a valid access token
        var loginClient = factory.CreateClient();
        var login = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginDoc.RootElement.GetProperty("accessToken").GetString();
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Licenses by tier
        var byTier = await authed.GetAsync("/api/v1/analytics/licenses-by-tier");
        byTier.EnsureSuccessStatusCode();
        var arr = JsonDocument.Parse(await byTier.Content.ReadAsStringAsync()).RootElement;
        Assert.True(arr.GetArrayLength() >= 2);
        var basic = arr.EnumerateArray().First(e => e.GetProperty("tier").GetString() == "Basic");
        Assert.Equal(2, basic.GetProperty("count").GetInt32());
        Assert.Equal(2, basic.GetProperty("activeCount").GetInt32());
        var pro = arr.EnumerateArray().First(e => e.GetProperty("tier").GetString() == "Pro");
        Assert.Equal(2, pro.GetProperty("count").GetInt32());
        Assert.Equal(1, pro.GetProperty("activeCount").GetInt32());

        // Tweaks failures top
        var failures = await authed.GetAsync("/api/v1/analytics/tweaks-failures-top?days=7&top=2");
        failures.EnsureSuccessStatusCode();
        var farr = JsonDocument.Parse(await failures.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Tweak A", farr[0].GetProperty("name").GetString());
        Assert.Equal(3, farr[0].GetProperty("failures").GetInt32());

        // Active devices within 60 minutes
        var active = await authed.GetAsync("/api/v1/analytics/active-devices?minutes=60");
        active.EnsureSuccessStatusCode();
        var adoc = JsonDocument.Parse(await active.Content.ReadAsStringAsync()).RootElement;
        Assert.True(adoc.GetProperty("count").GetInt32() >= 1);
        var ids = adoc.GetProperty("deviceIds").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("DEV-1", ids);
    }
}

