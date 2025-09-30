using GGs.Server.Data;
using GGs.Server.Services;
using GGs.Shared.Tweaks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/analytics")]
[Route("api/v1/analytics")]
[ApiExplorerSettings(GroupName = "v1")]
[Authorize(Roles = "Owner,Admin,Moderator,EnterpriseUser,ProUser")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DeviceRegistry _registry;

    public AnalyticsController(AppDbContext db, DeviceRegistry registry)
    {
        _db = db;
        _registry = registry;
    }

    // GET /api/analytics/summary
[HttpGet("summary")]
[ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "days" })]
public async Task<ActionResult<object>> Summary([FromQuery] int days = 7)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 90));
        var users = await _db.Users.CountAsync();
        var tweaks = await _db.Tweaks.CountAsync();
        var licenses = await _db.Licenses.CountAsync();
        var logsTotal = await _db.TweakLogs.CountAsync();
        var logsSince = await _db.TweakLogs.CountAsync(l => l.AppliedUtc >= since);
        var devicesConnected = _registry.GetDevices().Count;
        return Ok(new
        {
            users,
            tweaks,
            licenses,
            logsTotal,
            logsSince,
            devicesConnected,
            windowStartUtc = since
        });
    }

    // GET /api/analytics/tweaks
[HttpGet("tweaks")]
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "days", "top" })]
public async Task<ActionResult<IEnumerable<object>>> TweakStats([FromQuery] int days = 30, [FromQuery] int top = 20)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 365));
        top = Math.Clamp(top, 1, 100);
        var query = _db.TweakLogs.AsNoTracking().Where(l => l.AppliedUtc >= since);
        var agg = await query
            .GroupBy(l => new { l.TweakId, l.TweakName })
            .Select(g => new { g.Key.TweakId, Name = g.Key.TweakName, Count = g.Count(), Success = g.Count(x => x.Success), Fail = g.Count(x => !x.Success) })
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToListAsync();
        return Ok(agg);
    }

    // GET /api/analytics/devices
    [HttpGet("devices")]
    public ActionResult<IEnumerable<string>> Devices()
        => Ok(_registry.GetDevices());

    // GET /api/analytics/licenses-by-tier
[HttpGet("licenses-by-tier")]
[ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any)]
public async Task<ActionResult<IEnumerable<object>>> LicensesByTier()
    {
        var list = await _db.Licenses.AsNoTracking()
            .GroupBy(l => l.Tier)
            .Select(g => new
            {
                tier = g.Key,
                count = g.Count(),
                activeCount = g.Count(x => x.Status == "Active")
            })
            .OrderBy(x => x.tier)
            .ToListAsync();
        return Ok(list);
    }

    // GET /api/analytics/tweaks-failures-top?days=&top=
    [HttpGet("tweaks-failures-top")]
    public async Task<ActionResult<IEnumerable<object>>> TweaksFailuresTop([FromQuery] int days = 30, [FromQuery] int top = 20)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 365));
        top = Math.Clamp(top, 1, 100);
        var res = await _db.TweakLogs.AsNoTracking()
            .Where(l => l.AppliedUtc >= since && !l.Success)
            .GroupBy(l => new { l.TweakId, l.TweakName })
            .Select(g => new { g.Key.TweakId, Name = g.Key.TweakName, Failures = g.Count() })
            .OrderByDescending(x => x.Failures)
            .Take(top)
            .ToListAsync();
        return Ok(res);
    }

    // GET /api/analytics/active-devices?minutes=
    [HttpGet("active-devices")]
    public async Task<ActionResult<object>> ActiveDevices([FromQuery] int minutes = 60)
    {
        minutes = Math.Clamp(minutes, 1, 24 * 60);
        var since = DateTime.UtcNow.AddMinutes(-minutes);
        var devices = await _db.DeviceRegistrations.AsNoTracking()
            .Where(d => d.IsActive && d.LastSeenUtc >= since)
            .OrderByDescending(d => d.LastSeenUtc)
            .Select(d => d.DeviceId)
            .ToListAsync();
        return Ok(new { count = devices.Count, sinceUtc = since, deviceIds = devices });
    }
}

