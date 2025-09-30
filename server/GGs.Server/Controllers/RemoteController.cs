using GGs.Server.Data;
using GGs.Server.Hubs;
using GGs.Server.Services;
using GGs.Shared.Tweaks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[ApiExplorerSettings(GroupName = "v1")]
[Authorize(Policy = "ExecuteRemote")]
public class RemoteController : ControllerBase
{
    private readonly DeviceRegistry _registry;
    private readonly IHubContext<AdminHub> _hub;
    private readonly AppDbContext _db;
    private readonly IEntitlementsService _entitlements;

    public RemoteController(DeviceRegistry registry, IHubContext<AdminHub> hub, AppDbContext db, IEntitlementsService entitlements)
    {
        _registry = registry;
        _hub = hub;
        _db = db;
        _entitlements = entitlements;
    }

    [HttpGet("connections")]
    public ActionResult<IEnumerable<string>> Connections()
        => Ok(_registry.GetDevices());

    public sealed record ExecuteRequest(string DeviceId, Guid TweakId);

    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] ExecuteRequest req)
    {
        var conn = _registry.GetConnection(req.DeviceId);
        if (conn == null) return NotFound("Device not connected.");
        var tweak = await _db.Tweaks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TweakId);
        if (tweak == null) return NotFound("Tweak not found.");
        var ent = await _entitlements.ComputeAsync(User, HttpContext.RequestAborted);
        var allowed = tweak.Risk switch
        {
            GGs.Shared.Enums.RiskLevel.Low => ent.Tweaks.AllowLowRisk,
            GGs.Shared.Enums.RiskLevel.Medium => ent.Tweaks.AllowMediumRisk,
            GGs.Shared.Enums.RiskLevel.High => ent.Tweaks.AllowHighRisk,
            GGs.Shared.Enums.RiskLevel.Critical => ent.Tweaks.AllowExperimental,
            _ => false
        };
        if (!allowed)
            return Forbid();
        var correlationId = Guid.NewGuid().ToString();
        await _hub.Clients.Client(conn).SendAsync("ExecuteTweak", tweak, correlationId);
        return Accepted(new { correlationId });
    }
}
