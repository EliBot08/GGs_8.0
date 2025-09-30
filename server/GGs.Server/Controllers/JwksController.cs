using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/jwks")]
[Route("api/v1/jwks")]
[ApiExplorerSettings(GroupName = "v1")]
public sealed class JwksController : ControllerBase
{
    private readonly GGs.Server.Services.ISigningKeyProvider _signing;
    public JwksController(GGs.Server.Services.ISigningKeyProvider signing) { _signing = signing; }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        var keys = _signing.GetJwks();
        return Ok(new { keys });
    }
}

