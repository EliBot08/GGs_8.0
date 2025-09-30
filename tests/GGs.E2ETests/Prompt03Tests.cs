using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GGs.E2ETests;

public class Prompt03Tests
{
    [Fact]
    public async Task OnlineOffline_SummaryReflectsState()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        // login to get JWT for hub connection
        using var http = factory.CreateClient();
        var login = await http.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var data = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(data);
        var token = data!.accessToken;

        // connect to hub
        var baseAddr = http.BaseAddress!.ToString().TrimEnd('/');
        var hubUrl = baseAddr + "/hubs/admin";
        var deviceId = "e2e-device-" + Guid.NewGuid().ToString("N");
        var conn = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        await conn.StartAsync();
        await conn.InvokeAsync("RegisterDevice", deviceId);
        await conn.InvokeAsync("Heartbeat", deviceId);

        // online list should include device
        using var httpAuthed = factory.CreateClient();
        httpAuthed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var online = await httpAuthed.GetFromJsonAsync<string[]>("/api/devices/online");
        Assert.Contains(deviceId, online!);

        // force-expire by calling registry directly
        var registry = factory.Services.GetRequiredService<GGs.Server.Services.DeviceRegistry>();
        await Task.Delay(10); // ensure time advances beyond 0
        registry.ExpireStale(TimeSpan.Zero);

        var online2 = await httpAuthed.GetFromJsonAsync<string[]>("/api/devices/online");
        Assert.DoesNotContain(deviceId, online2!);

        await conn.DisposeAsync();
    }

    private sealed class LoginResponse
    {
        public string accessToken { get; set; } = string.Empty;
    }
}

