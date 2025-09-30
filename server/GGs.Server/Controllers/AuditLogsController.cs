using GGs.Server.Data;
using GGs.Shared.Tweaks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Obsolete("Deprecated. Use POST /api/audit/log with X-Machine-Token or client certificate.")]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuditLogsController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

[HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] TweakApplicationLog log)
    {
        // Security: allow if authenticated OR valid machine token OR client certificate present
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
            return Unauthorized("Missing authentication, valid client certificate, or machine token.");

        _db.TweakLogs.Add(log);
        await _db.SaveChangesAsync();
        return Ok(log);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Support")]
    public async Task<ActionResult<IEnumerable<TweakApplicationLog>>> Get()
        => Ok(await _db.TweakLogs.AsNoTracking().OrderByDescending(l => l.AppliedUtc).Take(500).ToListAsync());
}
