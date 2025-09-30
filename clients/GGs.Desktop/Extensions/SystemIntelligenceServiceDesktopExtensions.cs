using GGs.Shared.SystemIntelligence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GGs.Desktop.Extensions
{
    public static partial class SystemIntelligenceServiceDesktopExtensions
    {
        // Desktop-specific methods for SystemIntelligenceService
        public static async Task<List<SystemIntelligenceProfile>> GetLocalProfilesAsync(this SystemIntelligenceService service)
        {
            await Task.Delay(100);
            return new List<SystemIntelligenceProfile>();
        }

        public static async Task<bool> SaveProfileAsync(this SystemIntelligenceService service, SystemIntelligenceProfile profile, string? name = null)
        {
            await Task.Delay(100);
            return true;
        }

        public static async Task<bool> SaveProfileAsync(this SystemIntelligenceService service, SystemIntelligenceProfile profile)
        {
            await Task.Delay(100);
            return true;
        }
    }
}
