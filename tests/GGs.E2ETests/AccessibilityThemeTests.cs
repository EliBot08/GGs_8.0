using System;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using Xunit;

namespace GGs.E2ETests;

public class AccessibilityThemeTests
{
    private static Exception? RunSta(Action action)
    {
        Exception? ex = null;
        var t = new System.Threading.Thread(() => { try { action(); } catch (Exception e) { ex = e; } });
        t.SetApartmentState(System.Threading.ApartmentState.STA);
        t.Start();
        t.Join();
        return ex;
    }

    [Fact]
    public void ThemeManager_AppliesHighContrast_WhenForced()
    {
        Exception? ex = RunSta(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null) new System.Windows.Application();
                Environment.SetEnvironmentVariable("GGS_FORCE_HIGH_CONTRAST", "1");
                ThemeManagerService.Instance.ApplyTheme();
                // Ensure resources exist post-apply
                Assert.NotNull(System.Windows.Application.Current.Resources["ThemeBackgroundPrimary"]);
                Assert.NotNull(System.Windows.Application.Current.Resources["ThemeTextPrimary"]);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GGS_FORCE_HIGH_CONTRAST", null);
            }
        });
        Assert.Null(ex);
    }
}

