using Microsoft.AspNetCore.Identity;

namespace GGs.Server.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string? MetadataJson { get; set; }
}
