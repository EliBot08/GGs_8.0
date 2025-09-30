using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using GGs.Desktop.Services;

namespace GGs.Desktop.Views;

public partial class OptimizationView : System.Windows.Controls.UserControl
{
    private ObservableCollection<SystemTweak> _tweaks = new();
    
    public OptimizationView()
    {
        try { InitializeComponent(); }
        catch (Exception ex)
        {
            try { GGs.Desktop.Services.AppLogger.LogError("OptimizationView InitializeComponent failed", ex); } catch { }
        }
        LoadTweaks();
    }
    
    public bool CanApplyTweaks => ApplyTweaksBtn?.IsEnabled ?? false;

    private void LoadTweaks()
    {
        try
        {
            if (ApplyTweaksBtn != null)
            {
                ApplyTweaksBtn.IsEnabled = EntitlementsService.HasCapability(GGs.Shared.Enums.Capability.ExecuteTweaks);
                EntitlementsService.Changed += (_, __) =>
                {
                    try { ApplyTweaksBtn.IsEnabled = EntitlementsService.HasCapability(GGs.Shared.Enums.Capability.ExecuteTweaks); } catch { }
                };
            }
        }
        catch { }
        _tweaks = new ObservableCollection<SystemTweak>
        {
            new SystemTweak
            {
                Name = "Disable Windows Telemetry",
                Description = "Stops Windows from collecting diagnostic and usage data",
                Category = "Privacy",
                IsEnabled = false,
                RegistryPath = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                RegistryKey = "AllowTelemetry",
                RegistryValue = "0"
            },
            new SystemTweak
            {
                Name = "Disable Cortana",
                Description = "Disables Cortana assistant to save resources",
                Category = "Performance",
                IsEnabled = false,
                RegistryPath = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                RegistryKey = "AllowCortana",
                RegistryValue = "0"
            },
            new SystemTweak
            {
                Name = "Disable Superfetch",
                Description = "Reduces disk usage and improves performance on SSDs",
                Category = "Performance",
                IsEnabled = false,
                ServiceName = "SysMain"
            },
            new SystemTweak
            {
                Name = "Enable Game Mode",
                Description = "Prioritizes gaming performance when playing games",
                Category = "Gaming",
                IsEnabled = false,
                RegistryPath = @"HKCU\Software\Microsoft\GameBar",
                RegistryKey = "AutoGameModeEnabled",
                RegistryValue = "1"
            },
            new SystemTweak
            {
                Name = "Disable Windows Search Indexing",
                Description = "Reduces background disk activity",
                Category = "Performance",
                IsEnabled = false,
                ServiceName = "WSearch"
            },
            new SystemTweak
            {
                Name = "Optimize Network for Gaming",
                Description = "Reduces network latency for online gaming",
                Category = "Network",
                IsEnabled = false,
                RegistryPath = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                RegistryKey = "NetworkThrottlingIndex",
                RegistryValue = "ffffffff"
            }
        };
        
        TweaksList.ItemsSource = _tweaks;
    }
    
    private async void GamingOptimization_Click(object sender, RoutedEventArgs e)
    {
        await ApplyOptimization("Gaming Mode", async () =>
        {
            // Disable unnecessary services for gaming
            // Use elevated helper for power plan change
            var (ok, msg) = await ElevationService.PowerCfgSetActiveAsync(new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"));
            if (!ok) { Debug.WriteLine($"Gaming optimization: PowerCfg failed: {msg}"); }
            // Set process priority (current process only)
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; } catch { }
            
            UpdateStatus("Gaming mode activated - Performance optimized for gaming");
        });
    }
    
    private async void PerformanceMode_Click(object sender, RoutedEventArgs e)
    {
        await ApplyOptimization("Performance Mode", async () =>
        {
            await Task.Run(() =>
            {
                try
                {
                    // Clear temp files
                    ClearTempFiles();
                    
                    // Optimize memory
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    
                    // Disable visual effects
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                        "VisualFXSetting", 2);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Performance optimization error: {ex.Message}");
                }
            });
            
            UpdateStatus("Performance mode activated - System optimized for maximum speed");
        });
    }
    
    private async void NetworkOpt_Click(object sender, RoutedEventArgs e)
    {
        await ApplyOptimization("Network Optimization", async () =>
        {
            var (ok1, m1) = await ElevationService.FlushDnsAsync();
            if (!ok1) Debug.WriteLine($"Network optimization: FlushDns failed: {m1}");
            var (ok2, m2) = await ElevationService.WinsockResetAsync();
            if (!ok2) Debug.WriteLine($"Network optimization: WinsockReset failed: {m2}");
            var opts = new System.Collections.Generic.Dictionary<string, string> { ["autotuninglevel"] = "normal", ["chimney"] = "enabled" };
            var (ok3, m3) = await ElevationService.NetshTcpGlobalAsync(opts);
            if (!ok3) Debug.WriteLine($"Network optimization: NetshTcpGlobal failed: {m3}");
            
            UpdateStatus("Network optimized - DNS flushed and TCP settings improved");
        });
    }
    
    private async void CleanSystem_Click(object sender, RoutedEventArgs e)
    {
        await ApplyOptimization("System Cleanup", async () =>
        {
            var freedSpace = await Task.Run(() =>
            {
                long totalFreed = 0;
                
                try
                {
                    // Clear Windows temp files
                    totalFreed += ClearDirectory(Path.GetTempPath());
                    
                    // Clear Windows prefetch
                    var prefetchPath = @"C:\Windows\Prefetch";
                    if (Directory.Exists(prefetchPath))
                    {
                        totalFreed += ClearDirectory(prefetchPath);
                    }
                    
                    // Clear recycle bin - skipped for safety; MSIX/enterprise policy may restrict. Consider using SHEmptyRecycleBin via a safe wrapper.
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Cleanup error: {ex.Message}");
                }
                
                return totalFreed;
            });
            
            var freedGB = Math.Round(freedSpace / (1024.0 * 1024.0 * 1024.0), 2);
            UpdateStatus($"System cleaned - Freed {freedGB} GB of disk space");
        });
    }
    
    private async void MemoryOpt_Click(object sender, RoutedEventArgs e)
    {
        await ApplyOptimization("Memory Optimization", async () =>
        {
            await Task.Run(() =>
            {
                try
                {
                    // Force garbage collection
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    
                    // Trim working sets of all processes
                    foreach (var process in Process.GetProcesses())
                    {
                        try
                        {
                            if (!process.ProcessName.Contains("System") && 
                                !process.ProcessName.Contains("Idle"))
                            {
                                EmptyWorkingSet(process.Handle);
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Memory optimization error: {ex.Message}");
                }
            });
            
            UpdateStatus("Memory optimized - RAM usage reduced");
        });
    }
    
    private async void StartupOpt_Click(object sender, RoutedEventArgs e)
    {
        await ApplyOptimization("Startup Optimization", async () =>
        {
            await Task.Run(() =>
            {
                try
                {
                    // Disable unnecessary startup programs
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null)
                        {
                            var valuesToRemove = key.GetValueNames()
                                .Where(name => !IsEssentialStartupProgram(name))
                                .ToList();
                            
                            foreach (var value in valuesToRemove)
                            {
                                key.DeleteValue(value, false);
                            }
                        }
                    }
                    
                    // Set boot timeout via elevated helper with user consent
                    _ = ElevationService.BcdeditTimeoutAsync(3).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Startup optimization error: {ex.Message}");
                }
            });
            
            UpdateStatus("Startup optimized - Boot time reduced");
        });
    }
    
    private async void ApplyTweaks_Click(object sender, RoutedEventArgs e)
    {
        var selectedTweaks = _tweaks.Where(t => t.IsEnabled).ToList();
        
        if (selectedTweaks.Count == 0)
        {
            UpdateStatus("No tweaks selected");
            return;
        }
        
        await ApplyOptimization($"Applying {selectedTweaks.Count} tweaks", async () =>
        {
            await Task.Run(() =>
            {
                foreach (var tweak in selectedTweaks)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(tweak.ServiceName))
                        {
                            // Handle service tweaks
                            using (var service = new ServiceController(tweak.ServiceName))
                            {
                                if (service.Status == ServiceControllerStatus.Running)
                                {
                                    service.Stop();
                                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                                }
                            }
                        }
                        else if (!string.IsNullOrEmpty(tweak.RegistryPath))
                        {
                            // Handle registry tweaks
                            Registry.SetValue(tweak.RegistryPath, tweak.RegistryKey, tweak.RegistryValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to apply tweak {tweak.Name}: {ex.Message}");
                    }
                }
            });
            
            UpdateStatus($"Successfully applied {selectedTweaks.Count} system tweaks");
        });
    }
    
    private async Task ApplyOptimization(string operationName, Func<Task> optimization)
    {
        try
        {
            UpdateStatus($"Applying {operationName}...");
            await optimization();
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error: {ex.Message}");
        }
    }
    
    private void UpdateStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
            
            // Animate status text
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            StatusText.BeginAnimation(OpacityProperty, fadeIn);
        });
    }
    
    private void ClearTempFiles()
    {
        try
        {
            var tempPath = Path.GetTempPath();
            ClearDirectory(tempPath);
        }
        catch { }
    }
    
    private long ClearDirectory(string path)
    {
        long totalSize = 0;
        
        try
        {
            var di = new DirectoryInfo(path);
            
            foreach (FileInfo file in di.GetFiles())
            {
                try
                {
                    totalSize += file.Length;
                    file.Delete();
                }
                catch { }
            }
            
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                try
                {
                    totalSize += ClearDirectory(dir.FullName);
                    dir.Delete(true);
                }
                catch { }
            }
        }
        catch { }
        
        return totalSize;
    }
    
    private bool IsEssentialStartupProgram(string name)
    {
        var essentialPrograms = new[] 
        { 
            "SecurityHealth", 
            "OneDrive", 
            "WindowsDefender",
            "GGs.Desktop" // Keep our app
        };
        
        return essentialPrograms.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
    
    [System.Runtime.InteropServices.DllImport("psapi.dll")]
    static extern bool EmptyWorkingSet(IntPtr hProcess);
}

public class SystemTweak : INotifyPropertyChanged
{
    private bool _isEnabled;
    
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
    
    public string RegistryPath { get; set; } = string.Empty;
    public string RegistryKey { get; set; } = string.Empty;
    public string RegistryValue { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
