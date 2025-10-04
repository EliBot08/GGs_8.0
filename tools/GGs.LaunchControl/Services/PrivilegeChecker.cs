using System.Security.Principal;

namespace GGs.LaunchControl.Services;

/// <summary>
/// Checks current privilege level and elevation status.
/// </summary>
public static class PrivilegeChecker
{
    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current user name.
    /// </summary>
    public static string GetCurrentUser()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.Name;
        }
        catch
        {
            return Environment.UserName;
        }
    }

    /// <summary>
    /// Gets the integrity level of the current process.
    /// </summary>
    public static string GetIntegrityLevel()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                return "High (Administrator)";
            
            return "Medium (Standard User)";
        }
        catch
        {
            return "Unknown";
        }
    }
}

