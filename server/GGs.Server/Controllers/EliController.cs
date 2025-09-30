using GGs.Server.Services;
using GGs.Shared.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/eli")]
[Route("api/v1/eli")]
[ApiExplorerSettings(GroupName = "v1")]
[Authorize]
public sealed class EliController : ControllerBase
{
    private readonly IEntitlementsService _entitlements;
    private readonly ILogger<EliController> _logger;
    private readonly ILlmProvider _llm;

    public EliController(IEntitlementsService entitlements, ILogger<EliController> logger, ILlmProvider llm)
    {
        _entitlements = entitlements;
        _logger = logger;
        _llm = llm;
    }

    public sealed record AskRequest(string Question, string? ConversationId = null);
    public sealed record AskResponse(string Answer, object Quota, string Role, string Tier, string ConversationId);

    [HttpPost("ask")]
    [EnableRateLimiting("elibot-per-user")]
    public async Task<IActionResult> Ask([FromBody] AskRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Question)) return BadRequest("Question required");
        var ent = await _entitlements.ComputeAsync(User, HttpContext.RequestAborted);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var permit = ent.EliBot.DailyQuestionLimit;
        var today = DateTime.UtcNow.Date;
        // Persist usage counter (lightweight; for robust concurrency use an atomic SQL update)
        try
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GGs.Server.Data.AppDbContext>();
            var counter = await db.EliUsageCounters.FirstOrDefaultAsync(c => c.UserId == userId && c.DayUtc == today);
            if (counter == null)
            {
                counter = new GGs.Server.Models.EliUsageCounter { UserId = userId, DayUtc = today, Count = 0 };
                db.EliUsageCounters.Add(counter);
            }
            if (counter.Count >= (permit == int.MaxValue ? int.MaxValue : permit))
            {
                return StatusCode(429, new { message = "Daily Eli quota reached.", limit = permit });
            }
            counter.Count += 1;
            await db.SaveChangesAsync();
        }
        catch { }

        var convId = string.IsNullOrWhiteSpace(req.ConversationId) ? Guid.NewGuid().ToString("N") : req.ConversationId;
        var answer = await _llm.GenerateAsync($"[conv:{convId}] {req.Question}", HttpContext.RequestAborted);
        return Ok(new AskResponse(answer, new { used = "+1", daily = permit }, ent.RoleName, ent.Flags.TryGetValue("licenseTier", out var t) ? t.ToString() ?? "Unknown" : "Unknown", convId));
    }
}


