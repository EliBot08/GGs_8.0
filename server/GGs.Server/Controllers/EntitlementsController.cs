using GGs.Server.Services;
using GGs.Shared.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/entitlements")]
[Route("api/v1/entitlements")]
[ApiExplorerSettings(GroupName = "v1")]
[Authorize]
public sealed class EntitlementsController : ControllerBase
{
    private readonly IEntitlementsService _svc;
    private readonly ILogger<EntitlementsController> _logger;

    public EntitlementsController(IEntitlementsService svc, ILogger<EntitlementsController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<EntitlementsResponse>> Get()
    {
        var ent = await _svc.ComputeAsync(User, HttpContext.RequestAborted);
        return Ok(new EntitlementsResponse { Entitlements = ent, GeneratedUtc = DateTime.UtcNow });
    }
}


