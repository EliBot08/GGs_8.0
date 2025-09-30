using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GGs.Server.Data;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/admin")]
[Route("api/v1/admin")]
[ApiExplorerSettings(GroupName = "v1")]
[Authorize(Policy = "ManageUsers")] // Admin-only
public sealed class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    private readonly GGs.Server.Services.IAuditArchivalService _archival;

    public AdminController(AppDbContext db, GGs.Server.Services.IAuditArchivalService archival)
    {
        _db = db;
        _archival = archival;
    }

    [HttpGet("migrations")]
    public async Task<ActionResult<object>> GetMigrations()
    {
        var applied = await _db.Database.GetAppliedMigrationsAsync();
        var pending = await _db.Database.GetPendingMigrationsAsync();
        return Ok(new { applied, pending });
    }

    [HttpPost("run-archival")]
    public async Task<ActionResult<object>> RunArchival()
    {
        var (archived, filePath) = await _archival.RunOnceAsync(HttpContext.RequestAborted);
        return Ok(new { archived, filePath });
    }
}

