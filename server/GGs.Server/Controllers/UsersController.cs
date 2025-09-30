using GGs.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[ApiExplorerSettings(GroupName = "v1")]
[Authorize(Roles = "Owner,Admin")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<UsersController> _logger;
    private readonly IEmailSender _emailSender;

    public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ILogger<UsersController> logger, IEmailSender emailSender)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
        _emailSender = emailSender;
    }

    public sealed record CreateUserRequest(string Email, string Password, string[] Roles);
    public sealed record UserDto(string Id, string Email, string UserName, List<string> Roles, string? MetadataJson);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? q = null, [FromQuery] string? sort = "email", [FromQuery] bool desc = false)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 100);
        var query = _userManager.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var qv = q.Trim();
            query = query.Where(u => (u.Email != null && EF.Functions.Like(u.Email, $"%{qv}%")) || (u.UserName != null && EF.Functions.Like(u.UserName, $"%{qv}%")));
        }
        // Sorting
        sort = (sort ?? "email").ToLowerInvariant();
        if (sort == "username")
            query = desc ? query.OrderByDescending(u => u.UserName) : query.OrderBy(u => u.UserName);
        else
            query = desc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email);

        var total = query.Count();
        var pageItems = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var result = new List<UserDto>();
        foreach (var user in pageItems)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserDto(user.Id, user.Email ?? "", user.UserName ?? "", roles.ToList(), user.MetadataJson));
        }
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        var user = new ApplicationUser { UserName = req.Email, Email = req.Email, EmailConfirmed = true };
        var res = await _userManager.CreateAsync(user, req.Password);
        if (!res.Succeeded) return BadRequest(res.Errors);
        if (req.Roles?.Length > 0)
            await _userManager.AddToRolesAsync(user, req.Roles);
        return Ok(new { user.Id, user.Email });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var u = await _userManager.FindByIdAsync(id);
        if (u == null) return NotFound();
        await _userManager.DeleteAsync(u);
        return NoContent();
    }

    public sealed record RolesUpdateRequest(string[] Roles);

    [HttpPost("{id}/roles")]
    public async Task<IActionResult> SetRoles(string id, [FromBody] RolesUpdateRequest req)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        var allRoles = _roleManager.Roles.Select(r => r.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var desired = (req.Roles ?? Array.Empty<string>()).Where(r => allRoles.Contains(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var current = await _userManager.GetRolesAsync(user);
        var toRemove = current.Where(r => !desired.Contains(r, StringComparer.OrdinalIgnoreCase)).ToArray();
        var toAdd = desired.Where(r => !current.Contains(r, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (toRemove.Length > 0) await _userManager.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Length > 0) await _userManager.AddToRolesAsync(user, toAdd);
        _logger.LogInformation("Updated roles for user {UserId}: +{Add} -{Remove}", id, string.Join(',', toAdd), string.Join(',', toRemove));
        return NoContent();
    }

    [HttpPost("{id}/welcome-email")]
    public async Task<IActionResult> SendWelcomeEmail(string id)
    {
        var u = await _userManager.FindByIdAsync(id);
        if (u == null) return NotFound();
        var email = u.Email ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("User missing email");
        await _emailSender.SendWelcomeAsync(email, "Welcome to GGs", "Your account has been created.");
        return Accepted();
    }

    [HttpPost("import")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> ImportCsv()
    {
        if (!Request.HasFormContentType) return BadRequest("multipart/form-data required");
        var form = await Request.ReadFormAsync();
        var file = form.Files.FirstOrDefault();
        if (file == null) return BadRequest("File is required");
        if (file.Length == 0 || file.Length > 1_000_000) return BadRequest("Invalid file size");
        if (!string.Equals(file.ContentType, "text/csv", StringComparison.OrdinalIgnoreCase) && !string.Equals(file.ContentType, "application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Invalid content type");

        var created = new List<object>();
        var errors = new List<object>();
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var header = await reader.ReadLineAsync();
        int row = 1;
        while (!reader.EndOfStream)
        {
            row++;
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');
            if (parts.Length < 2) { errors.Add(new { row, error = "Missing columns" }); continue; }
            var email = parts[0].Trim();
            var password = parts[1].Trim();
            var rolesCell = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) { errors.Add(new { row, error = "Email or Password empty" }); continue; }
            try
            {
                var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
                var res = await _userManager.CreateAsync(user, password);
                if (!res.Succeeded) { errors.Add(new { row, error = string.Join(";", res.Errors.Select(e => e.Description)) }); continue; }
                var roles = rolesCell.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var validList = new List<string>();
                foreach (var r in roles)
                {
                    if (await _roleManager.RoleExistsAsync(r)) validList.Add(r);
                }
                if (validList.Count > 0) await _userManager.AddToRolesAsync(user, validList);
                created.Add(new { user.Id, user.Email, roles = validList });
            }
            catch (Exception ex)
            {
                errors.Add(new { row, error = ex.Message });
            }
        }
        return Ok(new { created, errors });
    }

    [HttpPost("{id}/suspend")]
    public async Task<IActionResult> Suspend(string id)
    {
        var u = await _userManager.FindByIdAsync(id);
        if (u == null) return NotFound();
        u.LockoutEnabled = true;
        u.LockoutEnd = DateTimeOffset.MaxValue;
        await _userManager.UpdateAsync(u);
        return NoContent();
    }

    [HttpPost("{id}/unsuspend")]
    public async Task<IActionResult> Unsuspend(string id)
    {
        var u = await _userManager.FindByIdAsync(id);
        if (u == null) return NotFound();
        u.LockoutEnd = null;
        await _userManager.UpdateAsync(u);
        return NoContent();
    }
}
