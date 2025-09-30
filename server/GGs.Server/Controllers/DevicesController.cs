using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using GGs.Server.Data;
using GGs.Server.Models;
using GGs.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[ApiExplorerSettings(GroupName = "v1")]
public sealed class DevicesController : ControllerBase
{
	private readonly IDeviceIdentityService _devices;
	private readonly AppDbContext _db;
	private readonly GGs.Server.Services.DeviceRegistry _registry;
	private readonly ILogger<DevicesController> _logger;
	private readonly IEntitlementsService _entitlements;

	public DevicesController(IDeviceIdentityService devices, AppDbContext db, GGs.Server.Services.DeviceRegistry registry, ILogger<DevicesController> logger, IEntitlementsService entitlements)
	{
		_devices = devices;
		_db = db;
		_registry = registry;
		_logger = logger;
		_entitlements = entitlements;
	}

	[HttpGet]
	[Authorize(Roles = "Admin,Manager,Support")]
	public async Task<IActionResult> Get([FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] bool? isActive = null, [FromQuery] string? q = null)
	{
		if (take > 100) take = 100;
		var query = _db.DeviceRegistrations.AsNoTracking().AsQueryable();
		if (isActive.HasValue) query = query.Where(d => d.IsActive == isActive.Value);
		if (!string.IsNullOrWhiteSpace(q)) query = query.Where(d => EF.Functions.Like(d.DeviceId, $"%{q}%"));
		var total = await query.CountAsync();
		var items = await query.OrderByDescending(d => d.LastSeenUtc).Skip(skip).Take(take).ToListAsync();
		Response.Headers["X-Total-Count"] = total.ToString();
		return Ok(items);
	}

	[HttpPost("enroll")]
	[Authorize]
	public async Task<IActionResult> Enroll([FromBody] DeviceEnrollRequest req)
	{
		if (string.IsNullOrWhiteSpace(req.DeviceId) || string.IsNullOrWhiteSpace(req.Thumbprint))
			return BadRequest("DeviceId and Thumbprint are required");

		// Enforce MaxDevices per entitlements for current user
		var ent = await _entitlements.ComputeAsync(User, HttpContext.RequestAborted);
		var max = Math.Max(1, ent.Enrollment.MaxDevices);
		var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
		var activeCount = await _db.UserDeviceAssignments.CountAsync(a => a.UserId == userId);
		if (activeCount >= max)
		{
			return StatusCode(403, new ProblemDetails { Title = "Max devices reached", Detail = $"Your plan allows up to {max} active devices.", Status = 403 });
		}

		var rec = await _devices.RegisterDeviceAsync(req.DeviceId, req.Thumbprint, req.CommonName);
		// Enforce that client presented a certificate when enabled
		if (HttpContext.RequestServices.GetRequiredService<IConfiguration>().GetValue<bool>("Security:ClientCertificate:Enabled"))
		{
			var cert = await HttpContext.Connection.GetClientCertificateAsync();
			if (cert == null)
			{
				return Unauthorized("Client certificate required for enrollment.");
			}
			if (!string.Equals(cert.GetCertHashString(), rec.Thumbprint, StringComparison.OrdinalIgnoreCase))
			{
				return Forbid("Certificate thumbprint mismatch.");
			}
		}

		// Assign tenant
		try
		{
			var tenantId = HttpContext.Items["TenantId"]?.ToString() ?? "default";
			rec.TenantId = tenantId;
			await _db.SaveChangesAsync();
			var assign = await _db.UserDeviceAssignments.FirstOrDefaultAsync(a => a.UserId == userId && a.DeviceId == rec.DeviceId);
			if (assign != null) { assign.TenantId = tenantId; await _db.SaveChangesAsync(); }
		}
		catch { }
		try
		{
			var assign = await _db.UserDeviceAssignments.FirstOrDefaultAsync(a => a.UserId == userId && a.DeviceId == rec.DeviceId);
			if (assign == null)
			{
				_db.UserDeviceAssignments.Add(new GGs.Server.Models.UserDeviceAssignment { UserId = userId, DeviceId = rec.DeviceId, AssignedUtc = DateTime.UtcNow });
				await _db.SaveChangesAsync();
			}
		}
		catch { }
		_logger.LogInformation("Device enrolled: {DeviceId} ({Thumb})", rec.DeviceId, rec.Thumbprint);
		return Ok(rec);
	}

	[HttpPost("rotate")]
	[Authorize]
	public async Task<IActionResult> Rotate([FromBody] DeviceEnrollRequest req)
	{
		if (string.IsNullOrWhiteSpace(req.DeviceId) || string.IsNullOrWhiteSpace(req.Thumbprint))
			return BadRequest("DeviceId and Thumbprint are required");
		var rec = await _devices.RegisterDeviceAsync(req.DeviceId, req.Thumbprint, req.CommonName);
		_logger.LogInformation("Device rotated cert: {DeviceId} ({Thumb})", rec.DeviceId, rec.Thumbprint);
		return Ok(rec);
	}

	[HttpPost("{deviceId}/revoke")]
	[Authorize(Roles = "Admin,Manager")]
	public async Task<IActionResult> Revoke(string deviceId)
	{
		var ok = await _devices.RevokeDeviceAsync(deviceId);
		if (!ok) return NotFound();
		_logger.LogWarning("Device revoked via API: {DeviceId}", deviceId);
		return Ok();
	}

	[HttpGet("online")]
	[Authorize(Roles = "Admin,Manager,Support")]
	public IActionResult Online()
	{
		var ids = _registry.GetDevices().ToArray();
		return Ok(ids);
	}

	[HttpGet("summary")]
	[Authorize(Roles = "Admin,Manager,Support")]
	public async Task<IActionResult> Summary()
	{
		var registered = await _db.DeviceRegistrations.CountAsync();
		var online = _registry.GetDevices().Count;
		return Ok(new { registered, online });
	}
}

public sealed record DeviceEnrollRequest
{
	[JsonPropertyName("deviceId")] public string DeviceId { get; init; } = string.Empty;
	[JsonPropertyName("thumbprint")] public string Thumbprint { get; init; } = string.Empty;
	[JsonPropertyName("commonName")] public string? CommonName { get; init; }
	[JsonPropertyName("certificateDerBase64")] public string? CertificateDerBase64 { get; init; }
}
