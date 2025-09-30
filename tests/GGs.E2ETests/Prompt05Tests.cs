using System.Net.Http.Headers;
using System.Net.Http.Json;
using GGs.Shared.Api;
using GGs.Shared.Tweaks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace GGs.E2ETests;

public class Prompt05Tests
{
    [Fact]
    public async Task RemoteExecute_EmitsCorrelation_AndBroadcastsAudit()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        using var http = factory.CreateClient();

        // Login admin to get token
        var login = await http.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var loginJson = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginJson);
        var token = loginJson!.accessToken;

        // Create a tweak to execute
        using var httpAuthed = factory.CreateClient();
        httpAuthed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createReq = new CreateTweakRequest
        {
            Name = "E2E_Remote",
            Description = "",
            Category = "Test",
            CommandType = 0,
            Safety = 3,
            Risk = 1,
            RequiresAdmin = false,
            AllowUndo = false
        };
        var created = await httpAuthed.PostAsJsonAsync("/api/tweaks", createReq);
        created.EnsureSuccessStatusCode();
        var tweakResp = await created.Content.ReadFromJsonAsync<TweakResponse>();
        Assert.NotNull(tweakResp);
        var tweakId = tweakResp!.Tweak!.Id;

        // Prepare hub connections
        var baseAddr = http.BaseAddress!.ToString().TrimEnd('/');
        var hubUrl = baseAddr + "/hubs/admin";
        var deviceId = "e2e-device-" + Guid.NewGuid().ToString("N");

        // Admin listener
        var adminConn = new HubConnectionBuilder()
            .WithUrl(hubUrl, o => o.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .WithAutomaticReconnect()
            .Build();
        TweakApplicationLog? receivedLog = null;
        string? receivedCorr = null;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        adminConn.On<TweakApplicationLog, string>("audit:added", (log, corr) => { receivedLog = log; receivedCorr = corr; tcs.TrySetResult(true); });
        await adminConn.StartAsync();

        // Agent simulator
        var agentConn = new HubConnectionBuilder()
            .WithUrl(hubUrl, o => o.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .WithAutomaticReconnect()
            .Build();
        await agentConn.StartAsync();
        await agentConn.InvokeAsync("RegisterDevice", deviceId);
        agentConn.On<TweakDefinition, string>("ExecuteTweak", async (t, corr) =>
        {
            // simulate success and report
            var log = new TweakApplicationLog
            {
                Id = Guid.NewGuid(),
                TweakId = t.Id,
                TweakName = t.Name,
                DeviceId = deviceId,
                AppliedUtc = DateTime.UtcNow,
                Success = true
            };
            await agentConn.InvokeAsync("ReportExecutionResult", log, corr);
        });

        // Execute via API
        var execResp = await httpAuthed.PostAsJsonAsync("/api/remote/execute", new { DeviceId = deviceId, TweakId = tweakId });
        Assert.Equal(System.Net.HttpStatusCode.Accepted, execResp.StatusCode);
        var body = await execResp.Content.ReadFromJsonAsync<CorrelationResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.correlationId));

        // Wait for admin broadcast
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5))) == tcs.Task && tcs.Task.Result;
        Assert.True(completed, "Did not receive audit:added broadcast in time");
        Assert.NotNull(receivedLog);
        Assert.Equal(tweakId, receivedLog!.TweakId);
        Assert.Equal(deviceId, receivedLog!.DeviceId);
        Assert.Equal(body!.correlationId, receivedCorr);

        await adminConn.DisposeAsync();
        await agentConn.DisposeAsync();
    }

    private sealed class LoginResponse { public string accessToken { get; set; } = string.Empty; }
    private sealed class CorrelationResponse { public string correlationId { get; set; } = string.Empty; }
}

