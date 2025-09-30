using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GGs.Server.Services;
using GGs.Shared.CloudProfiles;
using GGs.Shared.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[ApiExplorerSettings(GroupName = "v1")]
public sealed class ProfilesController : ControllerBase
{
    private readonly ICloudProfileStore _store;
    private readonly MarketplaceKeyService _keys;

    public ProfilesController(ICloudProfileStore store, MarketplaceKeyService keys)
    {
        _store = store; _keys = keys;
    }

    [HttpGet]
    public ActionResult<CloudProfilePage> GetAll([FromQuery]int page = 1, [FromQuery]int pageSize = 20)
    {
        page = page <= 0 ? 1 : page; pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
        var all = _store.GetAll().OrderByDescending(p => p.Payload.UpdatedUtc).ToList();
        var isAdmin = User?.IsInRole("Admin") == true;
        if (!isAdmin)
            all = all.Where(p => p.Payload.ModerationApproved).ToList();
        var items = all.Select(p => new CloudProfileSummary
        {
            Id = p.Payload.Id,
            Name = p.Payload.Name,
            Version = p.Payload.Version,
            Publisher = p.Payload.Publisher,
            UpdatedUtc = p.Payload.UpdatedUtc,
            Category = p.Payload.Category,
            ModerationApproved = p.Payload.ModerationApproved
        }).ToList();
        var total = items.Count;
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new CloudProfilePage { Page = page, PageSize = pageSize, Total = total, Items = pageItems });
    }

    [HttpPost("search")]
    public ActionResult<CloudProfilePage> Search([FromBody] CloudProfileSearchRequest req)
    {
        req.Page = req.Page <= 0 ? 1 : req.Page; req.PageSize = req.PageSize <= 0 ? 20 : Math.Min(req.PageSize, 100);
        var q = _store.GetAll().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(req.Query))
        {
            var qstr = req.Query.Trim();
            q = q.Where(p => p.Payload.Name.Contains(qstr, StringComparison.OrdinalIgnoreCase)
                          || p.Payload.Description.Contains(qstr, StringComparison.OrdinalIgnoreCase)
                          || p.Payload.Publisher.Contains(qstr, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(req.Category))
        {
            var cat = req.Category.Trim();
            q = q.Where(p => string.Equals(p.Payload.Category, cat, StringComparison.OrdinalIgnoreCase));
        }
        var isAdmin = User?.IsInRole("Admin") == true;
        if (!isAdmin)
            q = q.Where(p => p.Payload.ModerationApproved);
        var items = q.OrderByDescending(p => p.Payload.UpdatedUtc)
            .Select(p => new CloudProfileSummary
            {
                Id = p.Payload.Id,
                Name = p.Payload.Name,
                Version = p.Payload.Version,
                Publisher = p.Payload.Publisher,
                UpdatedUtc = p.Payload.UpdatedUtc,
                Category = p.Payload.Category,
                ModerationApproved = p.Payload.ModerationApproved
            }).ToList();
        var total = items.Count;
        var pageItems = items.Skip((req.Page - 1) * req.PageSize).Take(req.PageSize).ToList();
        return Ok(new CloudProfilePage { Page = req.Page, PageSize = req.PageSize, Total = total, Items = pageItems });
    }

    [HttpGet("{id}")]
    public ActionResult<SignedCloudProfile> Get(string id)
    {
        var p = _store.Get(id);
        if (p == null) return NotFound();
        return Ok(p);
    }

    [Authorize]
    [HttpPost]
    public ActionResult<SignedCloudProfile> Upload([FromBody] CloudProfilePayload payload)
    {
        payload.UpdatedUtc = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(payload.ContentHash))
        {
            try { payload.ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.Content ?? string.Empty))); } catch { }
        }
        var signed = SignPayload(payload);
        _store.Upsert(signed);
        return Ok(signed);
    }

    private SignedCloudProfile SignPayload(CloudProfilePayload payload)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_keys.PrivateKeyPem);
        var canonical = RsaLicenseService.CanonicalJson(payload);
        var data = Encoding.UTF8.GetBytes(canonical);
        var sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return new SignedCloudProfile
        {
            Payload = payload,
            Signature = Convert.ToBase64String(sig),
            KeyFingerprint = RsaLicenseService.ComputePublicKeyFingerprint(_keys.PublicKeyPem)
        };
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/approve")]
    public IActionResult Approve(string id)
    {
        var p = _store.Get(id);
        if (p == null) return NotFound();
        p.Payload.ModerationApproved = true;
        _store.Upsert(p);
        return NoContent();
    }
}

