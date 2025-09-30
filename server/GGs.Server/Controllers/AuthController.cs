using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GGs.Server.Data;
using GGs.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[ApiExplorerSettings(GroupName = "v1")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly SignInManager<ApplicationUser> _signInMgr;
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly GGs.Server.Services.ISigningKeyProvider _signing;
    private readonly GGs.Server.Services.IEntitlementsService _entitlements;

    public AuthController(UserManager<ApplicationUser> userMgr, SignInManager<ApplicationUser> signInMgr, AppDbContext db, IConfiguration cfg, GGs.Server.Services.ISigningKeyProvider signing, GGs.Server.Services.IEntitlementsService entitlements)
    {
        _userMgr = userMgr; _signInMgr = signInMgr; _db = db; _cfg = cfg; _signing = signing; _entitlements = entitlements;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _userMgr.FindByNameAsync(req.Username) ?? await _userMgr.FindByEmailAsync(req.Username);
        if (user == null) return Unauthorized("Invalid credentials");
        var pwOk = await _signInMgr.CheckPasswordSignInAsync(user, req.Password, true);
        if (!pwOk.Succeeded) return Unauthorized("Invalid credentials");

        var roles = await _userMgr.GetRolesAsync(user);
        var (token, expiresIn) = await IssueJwtAsync(user, roles);

        // Optionally issue refresh tokens (useful to disable in E2E tests)
        var issueRefresh = _cfg.GetValue<bool>("Auth:IssueRefreshTokens", true);
        if (issueRefresh)
        {
            var (refresh, refreshExp) = await IssueRefreshTokenAsync(user, req.DeviceId);
            return Ok(new { accessToken = token, expiresIn, refreshToken = refresh, refreshExpiresIn = refreshExp, roles });
        }
        else
        {
            return Ok(new { accessToken = token, expiresIn, roles });
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken)) return BadRequest("Missing token");
        var hash = Sha256(req.RefreshToken);
        var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (rt == null || rt.RevokedUtc.HasValue || rt.ExpiresUtc <= DateTime.UtcNow) return Unauthorized();
        var user = await _userMgr.FindByIdAsync(rt.UserId);
        if (user == null) return Unauthorized();
        var roles = await _userMgr.GetRolesAsync(user);
        // rotate refresh token
        rt.RevokedUtc = DateTime.UtcNow;
        var (newRefresh, refreshExp) = await IssueRefreshTokenAsync(user, req.DeviceId);
        var (access, expiresIn) = await IssueJwtAsync(user, roles);
        await _db.SaveChangesAsync();
        return Ok(new { accessToken = access, expiresIn, refreshToken = newRefresh, refreshExpiresIn = refreshExp, roles });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken)) return Ok();
        var hash = Sha256(req.RefreshToken);
        var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (rt != null && !rt.RevokedUtc.HasValue) { rt.RevokedUtc = DateTime.UtcNow; await _db.SaveChangesAsync(); }
        return Ok();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("revoke-jti")]
    public IActionResult RevokeJti([FromBody] RevokeJtiRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Jti)) return BadRequest();
        var exp = req.ExpiresUtc ?? DateTime.UtcNow.AddMinutes(60);
        HttpContext.RequestServices.GetRequiredService<IRevokedJtiStore>().Revoke(req.Jti, exp);
        return NoContent();
    }

    private async Task<(string token, int expiresIn)> IssueJwtAsync(ApplicationUser user, IList<string> roles)
    {
        var creds = _signing.GetCurrentSigningCredentials();
        var kid = _signing.GetCurrentKid();
        var issuer = _signing.GetIssuer();
        var audience = _signing.GetAudience();
        var accessMinutes = int.TryParse(_cfg["Auth:AccessTokenMinutes"], out var m) ? Math.Clamp(m, 1, 1440) : 30;
        var expires = DateTime.UtcNow.AddMinutes(accessMinutes);
        var jti = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        // Add EliBot quota limit as claim for quick client checks and API rate policies
        try
        {
            var ent = await _entitlements.ComputeForUserAsync(user.Id, roles);
            var limit = ent.EliBot.DailyQuestionLimit;
            claims.Add(new Claim("eli_limit", limit == int.MaxValue ? "unlimited" : limit.ToString()));
            claims.Add(new Claim("rbac_role", ent.RoleName));
            claims.Add(new Claim("license_tier", ent.Flags.TryGetValue("licenseTier", out var t) ? t.ToString() : "Unknown"));
        }
        catch { }
        var header = new JwtHeader(creds);
        header["kid"] = kid;
        var payload = new JwtPayload(issuer, audience, claims, notBefore: null, expires: expires);
        var token = new JwtSecurityToken(header, payload);
        var handler = new JwtSecurityTokenHandler();
        return (handler.WriteToken(token), (int)(expires - DateTime.UtcNow).TotalSeconds);
    }

    private async Task<(string token, int expires)> IssueRefreshTokenAsync(ApplicationUser user, string? deviceId)
    {
        var refreshMinutes = int.TryParse(_cfg["Auth:RefreshTokenMinutes"], out var mm) ? Math.Clamp(mm, 1, 60*24*365) : (60*24*90);
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes);
        var hash = Sha256(token);
        var rt = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(refreshMinutes),
            DeviceId = deviceId,
            UserAgent = Request.Headers["User-Agent"].ToString()
        };
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync();
        return (token, (int)(rt.ExpiresUtc - DateTime.UtcNow).TotalSeconds);
    }

    private static string Sha256(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    public sealed record LoginRequest(string Username, string Password, string? DeviceId);
    public sealed record RefreshRequest(string RefreshToken, string? DeviceId);
    public sealed record RevokeJtiRequest(string Jti, DateTime? ExpiresUtc);
}
