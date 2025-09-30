using System;

namespace GGs.Shared.Models;

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; } = true;
}
