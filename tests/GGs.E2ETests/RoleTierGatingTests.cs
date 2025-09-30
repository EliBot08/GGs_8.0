using System;
using Xunit;

namespace GGs.E2ETests;

public class RoleTierGatingTests
{
    [Fact]
    public void Entitlements_BasicSmoke()
    {
        GGs.Desktop.Services.EntitlementsService.Initialize(GGs.Shared.Enums.LicenseTier.Basic, Array.Empty<string>());
        // Smoke: ensure initialize path is resilient
        Assert.True(GGs.Desktop.Services.EntitlementsService.HasCapability(GGs.Desktop.Services.Capability.ViewDashboard));
    }
}

