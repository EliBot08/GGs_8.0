using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GGs.Desktop.Services;
using GGs.E2ETests.Infra;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt06Tests
{
    private static async Task<string> LoginAsync(WebApplicationFactory<GGs.Server.Program> factory)
    {
        using var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<LoginResponse>();
        return json!.accessToken;
    }

    [WpfFact]
    public async Task Analytics_Admin_UsesServer_NoBanner()
    {
        await using var factory = new TestAppFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var token = await LoginAsync(factory);
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var api = new ApiClient(http);
        var vm = new GGs.Desktop.ViewModels.AnalyticsViewModel(api);
        GGs.Desktop.Services.EntitlementsService.UpdateRoles(new[] { "Admin" });
        await vm.LoadAnalytics();

        Assert.True(vm.UsedServerAnalytics);
        Assert.True(string.IsNullOrWhiteSpace(vm.BannerMessage));
    }

    [WpfFact]
    public async Task Analytics_Unauthenticated_FallsBack_WithBanner()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var http = factory.CreateClient(); // no auth
        var api = new ApiClient(http);
        var vm = new GGs.Desktop.ViewModels.AnalyticsViewModel(api);
        GGs.Desktop.Services.EntitlementsService.UpdateRoles(new[] { "Admin" }); // allow attempt
        await vm.LoadAnalytics();

        Assert.False(vm.UsedServerAnalytics);
        Assert.False(string.IsNullOrWhiteSpace(vm.BannerMessage));
        Assert.Equal(0, vm.TotalTweaksApplied);
    }

    private sealed class LoginResponse { public string accessToken { get; set; } = string.Empty; }
}

