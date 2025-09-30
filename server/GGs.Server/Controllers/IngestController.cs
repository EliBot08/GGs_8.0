using System;
using System.Collections.Generic;
using System.Text.Json;
using GGs.Server.Data;
using GGs.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/ingest")] 
[Route("api/v1/ingest")] 
[ApiExplorerSettings(GroupName = "v1")]
public sealed class IngestController : ControllerBase
{
    public sealed record IngestEventDto(string eventId, string type, object payload, DateTime? createdUtc, string? client);

    private readonly AppDbContext _db;
    private readonly ILogger<IngestController> _logger;

    public IngestController(AppDbContext db, ILogger<IngestController> logger)
    {
        _db = db; _logger = logger;
    }

    [HttpPost("events")]
    [EnableRateLimiting("ingest-per-ip")]
    [RequestSizeLimit(2_000_000)] // ~2 MB
    public async Task<IActionResult> IngestEvents([FromBody] List<IngestEventDto> events)
    {
        var list = events ?? new List<IngestEventDto>();
        if (list.Count == 0) return Ok(new { accepted = 0 });
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var corr = HttpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var cid) ? cid.ToString() : null;
        var accepted = 0;
        foreach (var e in list)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(e.type)) continue;
                var payloadJson = JsonSerializer.Serialize(e.payload);
                var ev = new IngestEvent
                {
                    EventId = string.IsNullOrWhiteSpace(e.eventId) ? Guid.NewGuid().ToString("N") : e.eventId,
                    Type = e.type,
                    PayloadJson = payloadJson,
                    CreatedUtc = e.createdUtc ?? DateTime.UtcNow,
                    Client = e.client ?? ip
                };
                _db.IngestEvents.Add(ev);
                accepted++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to queue ingest event type={Type} corr={Corr}", e.type, corr);
            }
        }
        try { await _db.SaveChangesAsync(); } catch (Exception ex) { _logger.LogError(ex, "Saving ingest events failed"); }
        return Ok(new { accepted });
    }
}

