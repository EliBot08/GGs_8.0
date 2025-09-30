using GGs.Server.Data;
using GGs.Shared.Tweaks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/audit")]
[Route("api/v1/audit")]
[ApiExplorerSettings(GroupName = "v1")]
public sealed class AuditController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuditController> _logger;

    public AuditController(AppDbContext db, IConfiguration config, ILogger<AuditController> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Ingest a tweak execution audit log.
    /// Provide X-Machine-Token header if unauthenticated and no client certificate.
    /// </summary>
    /// <remarks>
    /// Security: Allowed if request is authenticated OR mTLS client certificate is present OR header X-Machine-Token matches configured Audit:MachineToken.
    /// </remarks>
    // POST /api/audit/log
    [HttpPost("log")]
    [AllowAnonymous]
    public async Task<IActionResult> Log([FromBody] TweakApplicationLog log)
    {
        if (log == null) return BadRequest("Missing log body");
        if (string.IsNullOrWhiteSpace(log.DeviceId)) return BadRequest("DeviceId required");
        if (log.AppliedUtc == default) log.AppliedUtc = DateTime.UtcNow;

        var isAuthenticated = HttpContext.User?.Identity?.IsAuthenticated == true;
        var hasCert = HttpContext.Connection?.ClientCertificate != null;
        var hasValidToken = false;
        try
        {
            var configured = _config["Audit:MachineToken"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (Request.Headers.TryGetValue("X-Machine-Token", out var header))
                {
                    hasValidToken = string.Equals(header.ToString(), configured, StringComparison.Ordinal);
                }
            }
        }
        catch { }

        if (!isAuthenticated && !hasCert && !hasValidToken)
        {
            return Unauthorized("Missing authentication, valid client certificate, or machine token.");
        }

        _db.TweakLogs.Add(log);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Audit log stored {Id} for device {DeviceId} tweak {TweakId} success={Success}", log.Id, log.DeviceId, log.TweakId, log.Success);
        return Ok(log);
    }

    // GET /api/audit/{id}
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Manager,Support")]
    public async Task<ActionResult<TweakApplicationLog>> GetById(Guid id)
    {
        var item = await _db.TweakLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    // GET /api/audit/search?deviceId=&userId=&tweakId=&from=&to=&success=&skip=&take=
    [HttpGet("search")]
    [Authorize(Roles = "Admin,Manager,Support")]
    public async Task<ActionResult<IEnumerable<TweakApplicationLog>>> Search(
        [FromQuery] string? deviceId,
        [FromQuery] string? userId,
        [FromQuery] Guid? tweakId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool? success,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 200)
    {
        take = Math.Clamp(take, 1, 1000);
        var q = _db.TweakLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(deviceId)) q = q.Where(x => x.DeviceId == deviceId);
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(x => x.UserId == userId);
        if (tweakId.HasValue) q = q.Where(x => x.TweakId == tweakId.Value);
        if (from.HasValue) q = q.Where(x => x.AppliedUtc >= from.Value);
        if (to.HasValue) q = q.Where(x => x.AppliedUtc <= to.Value);
        if (success.HasValue) q = q.Where(x => x.Success == success.Value);
        var items = await q.OrderByDescending(x => x.AppliedUtc).Skip(skip).Take(take).ToListAsync();
        return Ok(items);
    }
}

