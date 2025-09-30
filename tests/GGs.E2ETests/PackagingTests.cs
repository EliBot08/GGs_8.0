using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Xunit;

namespace GGs.E2ETests;

public class PackagingTests
{
    private static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "GGs.sln");
            if (File.Exists(candidate)) return dir;
            dir = Directory.GetParent(dir)!.FullName;
        }
        throw new InvalidOperationException("Solution root not found");
    }

    private static string RunPwsh(string scriptPath, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(60_000);
        if (p.ExitCode != 0)
        {
            throw new Exception($"Command failed ({p.ExitCode}): {stdout}\n{stderr}");
        }
        return stdout;
    }

    [Fact]
    public void Install_And_Uninstall_PerUser_Works_And_Toggles_Autostart()
    {
        var root = FindSolutionRoot();
        var installScript = Path.Combine(root, "packaging", "Install-GGsDesktop.ps1");
        Assert.True(File.Exists(installScript), installScript);

        // Use Desktop build output as source
        var sourceDir = Path.Combine(root, "clients", "GGs.Desktop", "bin", "Release", "net8.0-windows");
        Assert.True(Directory.Exists(sourceDir), sourceDir);

        var temp = Path.Combine(Path.GetTempPath(), "ggs_install_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            // Install with autostart enabled and no shortcut to avoid Start Menu side effects
RunPwsh(installScript, $"-SourceDir \"{sourceDir}\" -InstallDir \"{temp}\" -NoShortcut -Autostart -Channel stable");

            // Verify files exist
            Assert.True(File.Exists(Path.Combine(temp, "GGs.Desktop.exe")) || Directory.GetFiles(temp, "GGs.Desktop.exe", SearchOption.AllDirectories).Length > 0);

            // Verify Uninstall registry
            using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\GGs.Desktop", false))
            {
                Assert.NotNull(key);
                Assert.Equal(temp, key!.GetValue("InstallLocation") as string);
            }

            // Verify Autostart
            using (var run = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false))
            {
                var val = run?.GetValue("GGsDesktop") as string;
                Assert.False(string.IsNullOrWhiteSpace(val));
            }

            // Uninstall by running the generated Uninstall script
            var uninst = Path.Combine(temp, "Uninstall-GGsDesktop.ps1");
            Assert.True(File.Exists(uninst));
            RunPwsh(uninst, "-Silent");

            // Verify removal
            using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\GGs.Desktop", false))
            {
                Assert.Null(key);
            }
            using (var run = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false))
            {
                var val = run?.GetValue("GGsDesktop") as string;
                Assert.True(string.IsNullOrWhiteSpace(val));
            }
            Assert.False(Directory.Exists(temp));
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}

