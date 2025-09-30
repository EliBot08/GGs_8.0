using GGs.Server.Data;
using GGs.Server.Services;
using GGs.Shared.Tweaks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/tweaks/approvals")]
[Route("api/v1/tweaks/approvals")]
[ApiExplorerSettings(GroupName = "v1")]
[Authorize]
public sealed class TweakApprovalsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEntitlementsService _entitlements;

    public TweakApprovalsController(AppDbContext db, IEntitlementsService entitlements)
    {
        _db = db;
        _entitlements = entitlements;
    }

    public sealed record SubmitRequest(Guid TweakId, string Justification);
    public sealed record ApproveRequest(Guid TweakId, bool Approved, string? Reason);

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitRequest req)
    {
        var tweak = await _db.Tweaks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TweakId);
        if (tweak == null) return NotFound("Tweak not found");
        var ent = await _entitlements.ComputeAsync(User, HttpContext.RequestAborted);
        if (tweak.Risk < GGs.Shared.Enums.RiskLevel.High) return BadRequest("Approval not required for low/medium risk.");
        // Simulate queue: write a minimal record into IdempotencyRecords as placeholder
        _db.IdempotencyRecords.Add(new GGs.Server.Models.IdempotencyRecord 
        { 
            Key = $"tweak-approval-{req.TweakId}", 
            RequestHash = $"user-{User.Identity?.Name}", 
            ResponseJson = $"{{\"tweakId\":\"{tweak.Id}\",\"by\":\"{User.Identity?.Name}\",\"justification\":{System.Text.Json.JsonSerializer.Serialize(req.Justification)}}}", 
            StatusCode = 202,
            CreatedUtc = DateTime.UtcNow 
        });
        await _db.SaveChangesAsync();
        return Accepted();
    }

    [HttpPost("review")]
    [Authorize(Roles = "Owner,Admin,Moderator")]
    public async Task<IActionResult> Review([FromBody] ApproveRequest req)
    {
        var tweak = await _db.Tweaks.FirstOrDefaultAsync(t => t.Id == req.TweakId);
        if (tweak == null) return NotFound("Tweak not found");
        // For now, encode approval status into UpdatedUtc + Description suffix; real impl would store status table
        tweak.UpdatedUtc = DateTime.UtcNow;
        if (req.Approved)
        {
            tweak.Description = (tweak.Description ?? string.Empty) + "\n[Approved]";
        }
        else
        {
            tweak.Description = (tweak.Description ?? string.Empty) + $"\n[Rejected: {req.Reason}]";
        }
        await _db.SaveChangesAsync();
        return Ok();
    }
}


