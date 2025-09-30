using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GGs.Server.Models;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[ApiExplorerSettings(GroupName = "v1")]
[Authorize(Roles = "Admin")]
public sealed class RolesController : ControllerBase
{
    private readonly RoleManager<IdentityRole> _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<RolesController> _logger;

    public RolesController(RoleManager<IdentityRole> roles, UserManager<ApplicationUser> users, ILogger<RolesController> logger)
    {
        _roles = roles;
        _users = users;
        _logger = logger;
    }

    public sealed record RoleDto(string name, int userCount);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDto>>> Get()
    {
        var list = new List<RoleDto>();
        foreach (var role in _roles.Roles.OrderBy(r => r.Name))
        {
            var name = role.Name ?? string.Empty;
            try
            {
                var usersInRole = await _users.GetUsersInRoleAsync(name);
                list.Add(new RoleDto(name, usersInRole.Count));
            }
            catch
            {
                list.Add(new RoleDto(name, 0));
            }
        }
        return Ok(list);
    }
}

