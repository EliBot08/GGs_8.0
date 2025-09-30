using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GGs.Server.Models;
using GGs.Server.Data;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace GGs.Server.Controllers;

[ApiController]
[Route("scim/v2")]
[ApiExplorerSettings(GroupName = "scim")]
[Authorize(Policy = "ManageUsers")] // Only admins can manage SCIM
public sealed class ScimController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _db;
    private readonly ILogger<ScimController> _logger;

    public ScimController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext db,
        ILogger<ScimController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _logger = logger;
    }

    #region Users

    [HttpGet("Users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? filter = null,
        [FromQuery] int startIndex = 1,
        [FromQuery] int count = 100)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"]?.ToString() ?? "default";
            
            var query = _userManager.Users.AsQueryable();
            
            // Apply SCIM filter (simplified implementation)
            if (!string.IsNullOrEmpty(filter))
            {
                if (filter.Contains("userName eq"))
                {
                    var userName = ExtractFilterValue(filter, "userName eq");
                    query = query.Where(u => u.UserName == userName);
                }
                else if (filter.Contains("email eq"))
                {
                    var email = ExtractFilterValue(filter, "email eq");
                    query = query.Where(u => u.Email == email);
                }
            }

            var totalResults = await query.CountAsync();
            var users = await query
                .Skip(startIndex - 1)
                .Take(count)
                .ToListAsync();

            var scimUsers = new List<object>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                scimUsers.Add(new
                {
                    schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
                    id = user.Id,
                    userName = user.UserName,
                    name = new
                    {
                        familyName = user.UserName?.Split('@')[0] ?? "",
                        givenName = user.UserName?.Split('@')[0] ?? ""
                    },
                    emails = new[]
                    {
                        new
                        {
                            value = user.Email,
                            type = "work",
                            primary = true
                        }
                    },
                    active = !user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd < DateTimeOffset.UtcNow,
                    groups = roles.Select(r => new { value = r, display = r }),
                    meta = new
                    {
                        resourceType = "User",
                        created = user.LockoutEnd?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        lastModified = user.LockoutEnd?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        location = $"{Request.Scheme}://{Request.Host}/scim/v2/Users/{user.Id}"
                    }
                });
            }

            var response = new
            {
                schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:ListResponse" },
                totalResults,
                startIndex,
                itemsPerPage = scimUsers.Count,
                Resources = scimUsers
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SCIM users");
            return StatusCode(500, new { detail = "Internal server error" });
        }
    }

    [HttpGet("Users/{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound(new
                {
                    schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" },
                    detail = "User not found",
                    status = "404"
                });
            }

            var roles = await _userManager.GetRolesAsync(user);

            var scimUser = new
            {
                schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
                id = user.Id,
                userName = user.UserName,
                name = new
                {
                    familyName = user.UserName?.Split('@')[0] ?? "",
                    givenName = user.UserName?.Split('@')[0] ?? ""
                },
                emails = new[]
                {
                    new
                    {
                        value = user.Email,
                        type = "work",
                        primary = true
                    }
                },
                active = !user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd < DateTimeOffset.UtcNow,
                groups = roles.Select(r => new { value = r, display = r }),
                meta = new
                {
                    resourceType = "User",
                    created = user.LockoutEnd?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    lastModified = user.LockoutEnd?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    location = $"{Request.Scheme}://{Request.Host}/scim/v2/Users/{user.Id}"
                }
            };

            return Ok(scimUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SCIM user {UserId}", id);
            return StatusCode(500, new { detail = "Internal server error" });
        }
    }

    [HttpPost("Users")]
    public async Task<IActionResult> CreateUser([FromBody] ScimUser scimUser)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" },
                    detail = "Invalid user data",
                    status = "400"
                });
            }

            var tenantId = HttpContext.Items["TenantId"]?.ToString() ?? "default";

            // Check if user already exists
            var existingUser = await _userManager.FindByNameAsync(scimUser.UserName);
            if (existingUser != null)
            {
                return Conflict(new
                {
                    schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" },
                    detail = "User already exists",
                    status = "409"
                });
            }

            var user = new ApplicationUser
            {
                UserName = scimUser.UserName,
                Email = scimUser.Emails?.FirstOrDefault()?.Value ?? scimUser.UserName,
                EmailConfirmed = true,
                LockoutEnabled = !scimUser.Active
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" },
                    detail = string.Join(", ", result.Errors.Select(e => e.Description)),
                    status = "400"
                });
            }

            // Assign roles if specified
            if (scimUser.Groups?.Any() == true)
            {
                var rolesToAdd = scimUser.Groups
                    .Where(g => new[] { "Owner", "Admin", "Moderator", "EnterpriseUser", "ProUser", "BasicUser" }.Contains(g.Value))
                    .Select(g => g.Value)
                    .ToList();

                if (rolesToAdd.Any())
                {
                    await _userManager.AddToRolesAsync(user, rolesToAdd);
                }
            }
            else
            {
                // Default to BasicUser role
                await _userManager.AddToRoleAsync(user, "BasicUser");
            }

            _logger.LogInformation("SCIM user created: {UserName} (ID: {UserId}) in tenant {TenantId}", 
                user.UserName, user.Id, tenantId);

            var createdUser = new
            {
                schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
                id = user.Id,
                userName = user.UserName,
                name = new
                {
                    familyName = user.UserName?.Split('@')[0] ?? "",
                    givenName = user.UserName?.Split('@')[0] ?? ""
                },
                emails = new[]
                {
                    new
                    {
                        value = user.Email,
                        type = "work",
                        primary = true
                    }
                },
                active = !user.LockoutEnabled,
                meta = new
                {
                    resourceType = "User",
                    created = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    lastModified = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    location = $"{Request.Scheme}://{Request.Host}/scim/v2/Users/{user.Id}"
                }
            };

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, createdUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SCIM user");
            return StatusCode(500, new { detail = "Internal server error" });
        }
    }

    [HttpPut("Users/{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] ScimUser scimUser)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound(new
                {
                    schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" },
                    detail = "User not found",
                    status = "404"
                });
            }

            // Update user properties
            user.Email = scimUser.Emails?.FirstOrDefault()?.Value ?? user.Email;
            user.LockoutEnabled = !scimUser.Active;
            
            if (!scimUser.Active)
            {
                user.LockoutEnd = DateTimeOffset.MaxValue;
            }
            else
            {
                user.LockoutEnd = null;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" },
                    detail = string.Join(", ", result.Errors.Select(e => e.Description)),
                    status = "400"
                });
            }

            // Update roles
            if (scimUser.Groups?.Any() == true)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                var newRoles = scimUser.Groups
                    .Where(g => new[] { "Owner", "Admin", "Moderator", "EnterpriseUser", "ProUser", "BasicUser" }.Contains(g.Value))
                    .Select(g => g.Value)
                    .ToList();

                var rolesToRemove = currentRoles.Except(newRoles).ToList();
                var rolesToAdd = newRoles.Except(currentRoles).ToList();

                if (rolesToRemove.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                }

                if (rolesToAdd.Any())
                {
                    await _userManager.AddToRolesAsync(user, rolesToAdd);
                }
            }

            _logger.LogInformation("SCIM user updated: {UserName} (ID: {UserId})", user.UserName, user.Id);

            var updatedUser = new
            {
                schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
                id = user.Id,
                userName = user.UserName,
                name = new
                {
                    familyName = user.UserName?.Split('@')[0] ?? "",
                    givenName = user.UserName?.Split('@')[0] ?? ""
                },
                emails = new[]
                {
                    new
                    {
                        value = user.Email,
                        type = "work",
                        primary = true
                    }
                },
                active = !user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd < DateTimeOffset.UtcNow,
                meta = new
                {
                    resourceType = "User",
                    lastModified = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    location = $"{Request.Scheme}://{Request.Host}/scim/v2/Users/{user.Id}"
                }
            };

            return Ok(updatedUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SCIM user {UserId}", id);
            return StatusCode(500, new { detail = "Internal server error" });
        }
    }

    [HttpDelete("Users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound(new
                {
                    schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" },
                    detail = "User not found",
                    status = "404"
                });
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" },
                    detail = string.Join(", ", result.Errors.Select(e => e.Description)),
                    status = "400"
                });
            }

            _logger.LogInformation("SCIM user deleted: {UserName} (ID: {UserId})", user.UserName, user.Id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting SCIM user {UserId}", id);
            return StatusCode(500, new { detail = "Internal server error" });
        }
    }

    #endregion

    #region Groups

    [HttpGet("Groups")]
    public async Task<IActionResult> GetGroups()
    {
        try
        {
            var roles = await _roleManager.Roles.ToListAsync();
            var scimGroups = roles.Select(role => new
            {
                schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:Group" },
                id = role.Id,
                displayName = role.Name,
                members = new object[0], // Would need to populate with users in this role
                meta = new
                {
                    resourceType = "Group",
                    location = $"{Request.Scheme}://{Request.Host}/scim/v2/Groups/{role.Id}"
                }
            });

            var response = new
            {
                schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:ListResponse" },
                totalResults = roles.Count,
                startIndex = 1,
                itemsPerPage = roles.Count,
                Resources = scimGroups
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SCIM groups");
            return StatusCode(500, new { detail = "Internal server error" });
        }
    }

    #endregion

    #region Schema and ServiceProviderConfig

    [HttpGet("Schemas")]
    [AllowAnonymous]
    public IActionResult GetSchemas()
    {
        var schemas = new object[]
        {
            new
            {
                schemas = new string[] { "urn:ietf:params:scim:schemas:core:2.0:Schema" },
                id = "urn:ietf:params:scim:schemas:core:2.0:User",
                name = "User",
                description = "User Account",
                attributes = new object[]
                {
                    new { name = "userName", type = "string", required = true, caseExact = false, mutability = "readWrite", returned = "default", uniqueness = "server", multiValued = false },
                    new { name = "name", type = "complex", required = false, caseExact = false, mutability = "readWrite", returned = "default", uniqueness = "none", multiValued = false },
                    new { name = "emails", type = "complex", multiValued = true, required = false, caseExact = false, mutability = "readWrite", returned = "default", uniqueness = "none" },
                    new { name = "active", type = "boolean", required = false, caseExact = false, mutability = "readWrite", returned = "default", uniqueness = "none", multiValued = false }
                }
            }
        };

        return Ok(schemas);
    }

    [HttpGet("ServiceProviderConfig")]
    [AllowAnonymous]
    public IActionResult GetServiceProviderConfig()
    {
        var config = new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig" },
            documentationUri = $"{Request.Scheme}://{Request.Host}/docs",
            patch = new { supported = true },
            bulk = new { supported = false, maxOperations = 0, maxPayloadSize = 0 },
            filter = new { supported = true, maxResults = 1000 },
            changePassword = new { supported = false },
            sort = new { supported = false },
            etag = new { supported = false },
            authenticationSchemes = new[]
            {
                new
                {
                    type = "oauthbearertoken",
                    name = "OAuth Bearer Token",
                    description = "Authentication scheme using the OAuth Bearer Token Standard",
                    specUri = "http://www.rfc-editor.org/info/rfc6750",
                    documentationUri = $"{Request.Scheme}://{Request.Host}/docs/authentication"
                }
            }
        };

        return Ok(config);
    }

    #endregion

    #region Helper Methods

    private static string ExtractFilterValue(string filter, string attribute)
    {
        var index = filter.IndexOf(attribute, StringComparison.OrdinalIgnoreCase);
        if (index == -1) return "";

        var start = filter.IndexOf('"', index) + 1;
        var end = filter.IndexOf('"', start);
        
        return end > start ? filter.Substring(start, end - start) : "";
    }

    #endregion
}

#region DTOs

public sealed class ScimUser
{
    [Required]
    public string UserName { get; set; } = string.Empty;
    
    public ScimName? Name { get; set; }
    
    public List<ScimEmail>? Emails { get; set; }
    
    public bool Active { get; set; } = true;
    
    public List<ScimGroup>? Groups { get; set; }
}

public sealed class ScimName
{
    public string? FamilyName { get; set; }
    public string? GivenName { get; set; }
    public string? MiddleName { get; set; }
    public string? HonorificPrefix { get; set; }
    public string? HonorificSuffix { get; set; }
    public string? Formatted { get; set; }
}

public sealed class ScimEmail
{
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = "work";
    public bool Primary { get; set; } = true;
}

public sealed class ScimGroup
{
    public string Value { get; set; } = string.Empty;
    public string? Display { get; set; }
}

#endregion