using System.Text.Json;
using GGs.Server.Data;
using GGs.Server.Models;
using GGs.Shared.Api;
using GGs.Shared.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[ApiExplorerSettings(GroupName = "v1")]
public class LicensesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IOptionsSnapshot<LicenseKeysOptions> _keys;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LicensesController> _logger;

    public LicensesController(AppDbContext db, IOptionsSnapshot<LicenseKeysOptions> keys, UserManager<ApplicationUser> userManager, ILogger<LicensesController> logger)
    {
        _db = db;
        _keys = keys;
        _userManager = userManager;
        _logger = logger;
    }

[HttpGet]
    [Authorize(Policy = "ManageLicenses")]
    public async Task<ActionResult<IEnumerable<LicenseRecord>>> GetAll()
        => Ok(await _db.Licenses.AsNoTracking().ToListAsync());

[HttpPost("bootstrap-keys")]
    [Authorize(Policy = "ManageLicenses")]
    public ActionResult<object> BootstrapKeys()
    {
        var (priv, pub) = RsaLicenseService.CreateRsaKeyPair();
        return Ok(new
        {
            privateKeyPem = priv,
            publicKeyPem = pub,
            fingerprint = RsaLicenseService.ComputePublicKeyFingerprint(pub)
        });
    }

[HttpPost("issue")]
    [Authorize(Policy = "ManageLicenses")]
    [ServiceFilter(typeof(GGs.Server.Services.IdempotencyIssueFilter))]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("issue-per-user")]
    public async Task<ActionResult<LicenseIssueResponse>> Issue([FromBody] LicenseIssueRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.UserId))
            return ValidationProblem("UserId is required");
        var user = await _userManager.FindByIdAsync(req.UserId);
        if (user == null)
            return NotFound("User not found");
        if (req.ExpiresUtc.HasValue && req.ExpiresUtc.Value <= DateTime.UtcNow && !req.IsAdminKey)
            return ValidationProblem("ExpiresUtc must be in the future for non-admin keys");
        if (!string.IsNullOrWhiteSpace(req.DeviceBindingId) && req.DeviceBindingId!.Length > 256)
            return ValidationProblem("DeviceBindingId too long");

        var payload = new LicensePayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            UserId = req.UserId,
            Tier = req.Tier,
            IssuedUtc = DateTime.UtcNow,
            ExpiresUtc = req.IsAdminKey ? null : req.ExpiresUtc,
            IsAdminKey = req.IsAdminKey,
            DeviceBindingId = req.DeviceBindingId,
            AllowOfflineValidation = req.AllowOfflineValidation,
            Notes = req.Notes
        };

        var privPem = _keys.Value.PrivateKeyPem ?? string.Empty;
        if (string.IsNullOrWhiteSpace(privPem))
            return Problem(title: "License issuance unavailable", detail: "Server not configured with a private key.", statusCode: 503);

        try
        {
            var signed = RsaLicenseService.Sign(payload, privPem);
            var rec = new LicenseRecord
            {
                LicenseId = payload.LicenseId,
                UserId = payload.UserId,
                Tier = payload.Tier.ToString(),
                IssuedUtc = payload.IssuedUtc,
                ExpiresUtc = payload.ExpiresUtc,
                IsAdminKey = payload.IsAdminKey,
                DeviceBindingId = payload.DeviceBindingId,
                AllowOfflineValidation = payload.AllowOfflineValidation,
                SignedLicenseJson = JsonSerializer.Serialize(signed)
            };
            // Sensible defaults per tier
            rec.MaxDevices = payload.Tier switch
            {
                GGs.Shared.Enums.LicenseTier.Basic => 1,
                GGs.Shared.Enums.LicenseTier.Pro => 3,
                GGs.Shared.Enums.LicenseTier.Enterprise => 100,
                GGs.Shared.Enums.LicenseTier.Admin => 500,
                _ => 1
            };
            _db.Licenses.Add(rec);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Issued license {LicenseId} for user {UserId}", rec.LicenseId, rec.UserId);
            return Ok(new LicenseIssueResponse { License = signed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue license for user {UserId}", req.UserId);
            return Problem(title: "License issuance failed", detail: ex.Message, statusCode: 500);
        }
    }

[HttpPost("assign/{licenseId}/{userId}")]
    [Authorize(Policy = "ManageLicenses")]
    public async Task<IActionResult> Assign(string licenseId, string userId)
    {
        var rec = await _db.Licenses.FirstOrDefaultAsync(l => l.LicenseId == licenseId);
        if (rec == null) return NotFound();
        rec.UserId = userId;
        await _db.SaveChangesAsync();
        return Ok();
    }

[HttpPost("revoke/{licenseId}")]
    [Authorize(Policy = "ManageLicenses")]
    public async Task<IActionResult> Revoke(string licenseId)
    {
        var rec = await _db.Licenses.FirstOrDefaultAsync(l => l.LicenseId == licenseId);
        if (rec == null) return NotFound();
        rec.Status = "Revoked";
        await _db.SaveChangesAsync();
        return NoContent();
    }

[HttpPost("suspend/{licenseId}")]
    [Authorize(Policy = "ManageLicenses")]
    public async Task<IActionResult> Suspend(string licenseId)
    {
        var rec = await _db.Licenses.FirstOrDefaultAsync(l => l.LicenseId == licenseId);
        if (rec == null) return NotFound();
        rec.Status = "Suspended";
        await _db.SaveChangesAsync();
        return NoContent();
    }

[HttpPost("activate/{licenseId}")]
    [Authorize(Policy = "ManageLicenses")]
    public async Task<IActionResult> Activate(string licenseId)
    {
        var rec = await _db.Licenses.FirstOrDefaultAsync(l => l.LicenseId == licenseId);
        if (rec == null) return NotFound();
        rec.Status = "Active";
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public sealed record LicenseUpdateRequest(int? MaxDevices, bool? DeveloperMode, string? Notes);

[HttpPost("update/{licenseId}")]
    [Authorize(Policy = "ManageLicenses")]
    public async Task<IActionResult> Update(string licenseId, [FromBody] LicenseUpdateRequest req)
    {
        var rec = await _db.Licenses.FirstOrDefaultAsync(l => l.LicenseId == licenseId);
        if (rec == null) return NotFound();
        if (req.MaxDevices.HasValue) rec.MaxDevices = Math.Max(1, req.MaxDevices.Value);
        if (req.DeveloperMode.HasValue) rec.DeveloperMode = req.DeveloperMode.Value;
        if (req.Notes != null) rec.Notes = req.Notes;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    public ActionResult<LicenseValidateResponse> Validate([FromBody] LicenseValidateRequest req)
    {
        var pubPem = _keys.Value.PublicKeyPem ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pubPem))
            return Ok(new LicenseValidateResponse { IsValid = false, Message = "Server not configured with a public key." });

        var ok = RsaLicenseService.Verify(req.License, pubPem);
        if (!ok) return Ok(new LicenseValidateResponse { IsValid = false, Message = "Invalid signature." });

        var p = req.License.Payload;
        if (p.IsAdminKey)
            return Ok(new LicenseValidateResponse { IsValid = true, Message = "Admin key valid." });

        if (RsaLicenseService.IsExpired(p, DateTime.UtcNow))
            return Ok(new LicenseValidateResponse { IsValid = false, Message = "License expired." });

        if (!RsaLicenseService.IsDeviceMatch(p, req.CurrentDeviceBinding))
            return Ok(new LicenseValidateResponse { IsValid = false, Message = "Device mismatch." });

        // Enforcement based on server record if present
        var rec = _db.Licenses.FirstOrDefault(l => l.LicenseId == p.LicenseId);
        if (rec != null)
        {
            if (!string.Equals(rec.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return Ok(new LicenseValidateResponse { IsValid = false, Message = $"License {rec.Status}." });

            rec.UsageCount += 1;
            if (!string.IsNullOrWhiteSpace(p.DeviceBindingId))
            {
                try
                {
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(rec.AssignedDevicesJson) ?? new List<string>();
                    if (!list.Contains(p.DeviceBindingId, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(p.DeviceBindingId);
                        // dedup + cap 10
                        list = list.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
                    }
                    if (rec.MaxDevices > 0 && list.Count > rec.MaxDevices)
                    {
                        return Ok(new LicenseValidateResponse { IsValid = false, Message = "Max devices exceeded." });
                    }
                    rec.AssignedDevicesJson = System.Text.Json.JsonSerializer.Serialize(list);
                }
                catch { }
            }
            _db.SaveChanges();
        }

        return Ok(new LicenseValidateResponse { IsValid = true, Message = "License valid." });
    }
}
