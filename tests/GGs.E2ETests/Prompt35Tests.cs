using System;
using Xunit;

namespace GGs.E2ETests;

public class Prompt35Tests
{
    [Fact]
    public void Entitlements_Gates_TweakCapabilities_ByRole()
    {
        // Admin: all allowed
        GGs.Desktop.Services.EntitlementsService.Initialize(GGs.Shared.Enums.LicenseTier.Enterprise, new[] { "Admin" });
        Assert.True(GGs.Desktop.Services.EntitlementsService.HasCapability(GGs.Desktop.Services.Capability.ManageTweaks));
        Assert.True(GGs.Desktop.Services.EntitlementsService.HasCapability(GGs.Desktop.Services.Capability.ExecuteTweaks));

        // Manager: manage + execute allowed
        GGs.Desktop.Services.EntitlementsService.Initialize(GGs.Shared.Enums.LicenseTier.Enterprise, new[] { "Manager" });
        Assert.True(GGs.Desktop.Services.EntitlementsService.HasCapability(GGs.Desktop.Services.Capability.ManageTweaks));
        Assert.True(GGs.Desktop.Services.EntitlementsService.HasCapability(GGs.Desktop.Services.Capability.ExecuteTweaks));

        // Support: not allowed
        GGs.Desktop.Services.EntitlementsService.Initialize(GGs.Shared.Enums.LicenseTier.Enterprise, new[] { "Support" });
        Assert.False(GGs.Desktop.Services.EntitlementsService.HasCapability(GGs.Desktop.Services.Capability.ManageTweaks));
        Assert.False(GGs.Desktop.Services.EntitlementsService.HasCapability(GGs.Desktop.Services.Capability.ExecuteTweaks));
    }
}

