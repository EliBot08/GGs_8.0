using GGs.Server.Data;
using GGs.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Hubs;

[Authorize]
public sealed class AdminHub : Hub
{
    private readonly DeviceRegistry _registry;
    private readonly AppDbContext _db;
    private readonly ILogger<AdminHub> _logger;
    public AdminHub(DeviceRegistry registry, AppDbContext db, ILogger<AdminHub> logger)
    {
        _registry = registry;
        _db = db;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            var u = Context.User;
            if (u != null && (u.IsInRole("Admin") || u.IsInRole("Manager") || u.IsInRole("Support")))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
            }
        }
        catch { }
        await base.OnConnectedAsync();
    }

    public Task RegisterDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) throw new HubException("deviceId required");
        if (deviceId.Length is < 3 or > 128) throw new HubException("deviceId length invalid");
        foreach (var ch in deviceId)
        {
            if (!(char.IsLetterOrDigit(ch) || ch is '-' or '_' or ':' or '.'))
                throw new HubException("deviceId has invalid characters");
        }
        _registry.Register(deviceId, Context.ConnectionId);
        _logger.LogInformation("Device registered {DeviceId} conn {Conn}", deviceId, Context.ConnectionId);
        return Task.CompletedTask;
    }

    public async Task Heartbeat(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;
        _registry.Heartbeat(deviceId);
        var rec = await _db.DeviceRegistrations.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (rec != null)
        {
            rec.LastSeenUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task ReportExecutionResult(GGs.Shared.Tweaks.TweakApplicationLog log, string correlationId)
    {
        if (log.AppliedUtc == default) log.AppliedUtc = DateTime.UtcNow;
        _db.TweakLogs.Add(log);
        await _db.SaveChangesAsync();
        await Clients.Group("admins").SendAsync("audit:added", log, correlationId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _registry.UnregisterByConnection(Context.ConnectionId);
        if (exception != null)
            _logger.LogWarning(exception, "Hub disconnected {Conn}", Context.ConnectionId);
        else
            _logger.LogInformation("Hub disconnected {Conn}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
