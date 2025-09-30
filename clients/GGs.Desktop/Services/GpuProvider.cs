using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace GGs.Desktop.Services;

public interface IGpuProvider
{
    Task<float> GetGpuUsageAsync(); // 0-100; return -1 if unavailable
}

public sealed class WindowsCounterGpuProvider : IGpuProvider
{
    public Task<float> GetGpuUsageAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                // Sum utilization across GPU Engine instances (3D + Compute)
                var category = new PerformanceCounterCategory("GPU Engine");
                var instances = category.GetInstanceNames();
                var counters = new List<PerformanceCounter>();
                foreach (var inst in instances)
                {
                    if (!(inst.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase) ||
                          inst.Contains("engtype_Compute", StringComparison.OrdinalIgnoreCase)))
                        continue;
                    try
                    {
                        counters.Add(new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, readOnly: true));
                    }
                    catch { }
                }
                if (counters.Count == 0) return -1f;
                float total = 0;
                foreach (var c in counters)
                {
                    try { total += c.NextValue(); } catch { }
                }
                // Clamp 0..100
                if (total < 0) total = 0;
                if (total > 100) total = 100;
                return total;
            }
            catch { return -1f; }
        });
    }
}

