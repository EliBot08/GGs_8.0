using System;

namespace GGs.Agent.Services;

public partial class RealGpuDetectionService
{
    private bool CheckVulkanSupport()
    {
        try
        {
            var vulkanDll = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vulkan-1.dll");
            return System.IO.File.Exists(vulkanDll);
        }
        catch
        {
            return false;
        }
    }
}
