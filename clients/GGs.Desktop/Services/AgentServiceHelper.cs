using System;
using System.Linq;
using System.ServiceProcess;

namespace GGs.Desktop.Services;

public static class AgentServiceHelper
{
    private const string ServiceName = "GGsAgent";

    public static bool TryGetStatus(out bool exists, out bool running)
    {
        exists = false; running = false;
        try
        {
            var services = ServiceController.GetServices();
            var svc = services.FirstOrDefault(s => string.Equals(s.ServiceName, ServiceName, StringComparison.OrdinalIgnoreCase));
            if (svc == null) return false;
            exists = true;
            running = svc.Status == ServiceControllerStatus.Running;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsRunning()
    {
        try
        {
            return TryGetStatus(out var exists, out var running) && exists && running;
        }
        catch { return false; }
    }
}
