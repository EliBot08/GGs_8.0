using System;

namespace GGs.Desktop.Services;

public static class GatingService
{
    private static string[] _roles = Array.Empty<string>();
    public static event EventHandler<string[]?>? RolesChanged;

    public static void UpdateRoles(string[] roles)
    {
        _roles = roles ?? Array.Empty<string>();
        RolesChanged?.Invoke(null, _roles);
    }

    public static bool HasRole(string role) => Array.Exists(_roles, r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    public static bool IsAdmin => HasRole("Admin") || HasRole("Administrator");
}

