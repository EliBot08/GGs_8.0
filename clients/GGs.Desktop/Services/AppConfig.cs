using System;

namespace GGs.Desktop.Services;

public static class AppConfig
{
    public static bool DemoMode
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("GGS_DEMO_MODE");
            if (bool.TryParse(env, out var flag)) return flag;
            return false; // default to production behavior
        }
    }
}

