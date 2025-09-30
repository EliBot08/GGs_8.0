using System.Security.Cryptography;
using System.Text;
using GGs.Server.Data;
using GGs.Shared.Api;
using GGs.Shared.Tweaks;
using Microsoft.AspNetCore.Authorization;
using GGs.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[ApiExplorerSettings(GroupName = "v1")]
[Authorize]
public class TweaksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEntitlementsService _entitlements;

    public TweaksController(AppDbContext db, IEntitlementsService entitlements)
    {
        _db = db;
        _entitlements = entitlements;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Support")]
    public async Task<ActionResult<IEnumerable<TweakDefinition>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? q = null, [FromQuery] string? sort = "updated", [FromQuery] bool desc = true)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 100);
        var query = _db.Tweaks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var qv = q.Trim();
            query = query.Where(t => (t.Name != null && EF.Functions.Like(t.Name, $"%{qv}%"))
                                  || (t.Description != null && EF.Functions.Like(t.Description, $"%{qv}%"))
                                  || (t.Category != null && EF.Functions.Like(t.Category, $"%{qv}%")));
        }
        sort = (sort ?? "updated").ToLowerInvariant();
        if (sort == "name")
            query = desc ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name);
        else if (sort == "created")
            query = desc ? query.OrderByDescending(t => t.CreatedUtc) : query.OrderBy(t => t.CreatedUtc);
        else
            query = desc ? query.OrderByDescending(t => t.UpdatedUtc ?? t.CreatedUtc) : query.OrderBy(t => t.UpdatedUtc ?? t.CreatedUtc);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Manager,Support")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var t = await _db.Tweaks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        var etag = ComputeTweakETag(t);
        if (Request.Headers.TryGetValue("If-None-Match", out var inm))
        {
            var clientTag = inm.ToString();
            if (!string.IsNullOrWhiteSpace(clientTag) && string.Equals(clientTag, etag, StringComparison.Ordinal))
            {
                Response.Headers["ETag"] = etag;
                return StatusCode(304);
            }
        }
        Response.Headers["ETag"] = etag;
        return Ok(t);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Moderator")] // tighter control, aligns with new role set
    public async Task<ActionResult<TweakResponse>> Create([FromBody] CreateTweakRequest req)
    {
        var ent = await _entitlements.ComputeAsync(User);
        if (req.Risk == GGs.Shared.Enums.RiskLevel.High && !ent.Tweaks.AllowHighRisk) return Forbid();
        if (req.Risk == GGs.Shared.Enums.RiskLevel.Critical && !ent.Tweaks.AllowExperimental) return Forbid();

        var tweak = new TweakDefinition
        {
            Name = req.Name,
            Description = req.Description,
            Category = req.Category,
            CommandType = req.CommandType,
            RegistryPath = req.RegistryPath,
            RegistryValueName = req.RegistryValueName,
            RegistryValueType = req.RegistryValueType,
            RegistryValueData = req.RegistryValueData,
            ServiceName = req.ServiceName,
            ServiceAction = req.ServiceAction,
            ScriptContent = req.ScriptContent,
            Safety = req.Safety,
            Risk = req.Risk,
            RequiresAdmin = req.RequiresAdmin,
            AllowUndo = req.AllowUndo,
            UndoScriptContent = req.UndoScriptContent,
            CreatedUtc = DateTime.UtcNow
        };
        _db.Tweaks.Add(tweak);
        await _db.SaveChangesAsync();
        return Ok(new TweakResponse { Tweak = tweak });
    }

    [HttpPut]
    [Authorize(Roles = "Owner,Admin,Moderator")] // tighter control, aligns with new role set
    public async Task<ActionResult<TweakResponse>> Update([FromBody] UpdateTweakRequest req)
    {
        var ent = await _entitlements.ComputeAsync(User);
        if (req.Risk == GGs.Shared.Enums.RiskLevel.High && !ent.Tweaks.AllowHighRisk) return Forbid();
        if (req.Risk == GGs.Shared.Enums.RiskLevel.Critical && !ent.Tweaks.AllowExperimental) return Forbid();

        var t = await _db.Tweaks.FirstOrDefaultAsync(x => x.Id == req.Id);
        if (t == null) return NotFound();
        t.Name = req.Name;
        t.Description = req.Description;
        t.Category = req.Category;
        t.CommandType = req.CommandType;
        t.RegistryPath = req.RegistryPath;
        t.RegistryValueName = req.RegistryValueName;
        t.RegistryValueType = req.RegistryValueType;
        t.RegistryValueData = req.RegistryValueData;
        t.ServiceName = req.ServiceName;
        t.ServiceAction = req.ServiceAction;
        t.ScriptContent = req.ScriptContent;
        t.Safety = req.Safety;
        t.Risk = req.Risk;
        t.RequiresAdmin = req.RequiresAdmin;
        t.AllowUndo = req.AllowUndo;
        t.UndoScriptContent = req.UndoScriptContent;
        t.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var etag = ComputeTweakETag(t);
        Response.Headers["ETag"] = etag;
        return Ok(new TweakResponse { Tweak = t });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner,Admin")] // destructive action reserved for Owner/Admin
    public async Task<IActionResult> Delete(Guid id)
    {
        var t = await _db.Tweaks.FindAsync(id);
        if (t == null) return NotFound();
        _db.Tweaks.Remove(t);
        await _db.SaveChangesAsync();
        return NoContent();
    }
    private static string ComputeTweakETag(TweakDefinition t)
    {
        // Hash of Id + UpdatedUtc/CreatedUtc for deterministic ETag
        var basis = $"{t.Id:N}|{(t.UpdatedUtc?.ToUniversalTime().Ticks.ToString() ?? t.CreatedUtc.ToUniversalTime().Ticks.ToString())}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(basis));
        var tag = Convert.ToBase64String(hash);
        return $"\"{tag}\""; // strong ETag with quotes
    }
}
