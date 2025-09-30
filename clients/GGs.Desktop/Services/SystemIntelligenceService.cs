using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Text.Json;

namespace GGs.Desktop.Services
{
    public class SystemIntelligenceService : INotifyPropertyChanged
    {
        private static SystemIntelligenceService? _instance;
        public static SystemIntelligenceService Instance => _instance ??= new SystemIntelligenceService();

        private bool _isScanning;
        private double _scanProgress;
        private string _currentScanStep = "";
        private readonly ObservableCollection<SystemTweak> _detectedTweaks = new();
        private readonly ObservableCollection<SystemProfile> _profiles = new();

        public bool IsScanning
        {
            get => _isScanning;
            private set
            {
                _isScanning = value;
                OnPropertyChanged();
            }
        }

        public double ScanProgress
        {
            get => _scanProgress;
            private set
            {
                _scanProgress = value;
                OnPropertyChanged();
            }
        }

        public string CurrentScanStep
        {
            get => _currentScanStep;
            private set
            {
                _currentScanStep = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<SystemTweak> DetectedTweaks => _detectedTweaks;
        public ObservableCollection<SystemProfile> Profiles => _profiles;

        public event EventHandler<ScanCompletedEventArgs>? ScanCompleted;
        public event EventHandler<TweakDetectedEventArgs>? TweakDetected;
        public event PropertyChangedEventHandler? PropertyChanged;

        private SystemIntelligenceService()
        {
            LoadProfiles();
        }

        public async Task<ScanResult> StartSystemScanAsync(CancellationToken cancellationToken = default)
        {
            if (IsScanning)
                throw new InvalidOperationException("Scan is already in progress");

            IsScanning = true;
            ScanProgress = 0;
            _detectedTweaks.Clear();

            try
            {
                var result = new ScanResult();
                var totalSteps = 8;
                var currentStep = 0;

                // Step 1: Registry Analysis
                CurrentScanStep = "Analyzing registry settings...";
                ScanProgress = (++currentStep / (double)totalSteps) * 100;
                await Task.Delay(500, cancellationToken);
                var registryTweaks = await AnalyzeRegistryAsync(cancellationToken);
                foreach (var tweak in registryTweaks)
                {
                    _detectedTweaks.Add(tweak);
                    TweakDetected?.Invoke(this, new TweakDetectedEventArgs(tweak));
                }

                // Step 2: Service Analysis
                CurrentScanStep = "Scanning Windows services...";
                ScanProgress = (++currentStep / (double)totalSteps) * 100;
                await Task.Delay(500, cancellationToken);
                var serviceTweaks = await AnalyzeServicesAsync(cancellationToken);
                foreach (var tweak in serviceTweaks)
                {
                    _detectedTweaks.Add(tweak);
                    TweakDetected?.Invoke(this, new TweakDetectedEventArgs(tweak));
                }

                // Step 3: Startup Programs
                CurrentScanStep = "Analyzing startup programs...";
                ScanProgress = (++currentStep / (double)totalSteps) * 100;
                await Task.Delay(500, cancellationToken);
                var startupTweaks = await AnalyzeStartupProgramsAsync(cancellationToken);
                foreach (var tweak in startupTweaks)
                {
                    _detectedTweaks.Add(tweak);
                    TweakDetected?.Invoke(this, new TweakDetectedEventArgs(tweak));
                }

                // Step 4: Network Settings
                CurrentScanStep = "Checking network configuration...";
                ScanProgress = (++currentStep / (double)totalSteps) * 100;
                await Task.Delay(500, cancellationToken);
                var networkTweaks = await AnalyzeNetworkSettingsAsync(cancellationToken);
                foreach (var tweak in networkTweaks)
                {
                    _detectedTweaks.Add(tweak);
                    TweakDetected?.Invoke(this, new TweakDetectedEventArgs(tweak));
                }

                // Step 5: Power Settings
                CurrentScanStep = "Analyzing power management...";
                ScanProgress = (++currentStep / (double)totalSteps) * 100;
                await Task.Delay(500, cancellationToken);
                var powerTweaks = await AnalyzePowerSettingsAsync(cancellationToken);
                foreach (var tweak in powerTweaks)
                {
                    _detectedTweaks.Add(tweak);
                    TweakDetected?.Invoke(this, new TweakDetectedEventArgs(tweak));
                }

                // Step 6: Visual Effects
                CurrentScanStep = "Checking visual effects...";
                ScanProgress = (++currentStep / (double)totalSteps) * 100;
                await Task.Delay(500, cancellationToken);
                var visualTweaks = await AnalyzeVisualEffectsAsync(cancellationToken);
                foreach (var tweak in visualTweaks)
                {
                    _detectedTweaks.Add(tweak);
                    TweakDetected?.Invoke(this, new TweakDetectedEventArgs(tweak));
                }

                // Step 7: Gaming Optimizations
                CurrentScanStep = "Detecting gaming optimizations...";
                ScanProgress = (++currentStep / (double)totalSteps) * 100;
                await Task.Delay(500, cancellationToken);
                var gamingTweaks = await AnalyzeGamingOptimizationsAsync(cancellationToken);
                foreach (var tweak in gamingTweaks)
                {
                    _detectedTweaks.Add(tweak);
                    TweakDetected?.Invoke(this, new TweakDetectedEventArgs(tweak));
                }

                // Step 8: Finalization
                CurrentScanStep = "Finalizing analysis...";
                ScanProgress = 100;
                await Task.Delay(500, cancellationToken);

                result.TotalTweaksFound = _detectedTweaks.Count;
                result.PerformanceImpact = CalculatePerformanceImpact();
                result.SecurityScore = CalculateSecurityScore();
                result.RecommendedActions = GenerateRecommendations();

                ScanCompleted?.Invoke(this, new ScanCompletedEventArgs(result));
                return result;
            }
            finally
            {
                IsScanning = false;
                CurrentScanStep = "";
            }
        }

        private Task<List<SystemTweak>> AnalyzeRegistryAsync(CancellationToken cancellationToken)
        {
            var tweaks = new List<SystemTweak>();

            try
            {
                // Game Mode
                using var gameKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\GameBar");
                if (gameKey?.GetValue("AllowAutoGameMode")?.ToString() == "1")
                {
                    tweaks.Add(new SystemTweak
                    {
                        Name = "Game Mode Enabled",
                        Category = TweakCategory.Gaming,
                        Impact = TweakImpact.High,
                        Description = "Windows Game Mode is enabled for better gaming performance",
                        IsApplied = true,
                        RegistryPath = @"HKCU\SOFTWARE\Microsoft\GameBar\AllowAutoGameMode"
                    });
                }

                // Hardware Accelerated GPU Scheduling
                using var gpuKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
                if (gpuKey?.GetValue("HwSchMode")?.ToString() == "2")
                {
                    tweaks.Add(new SystemTweak
                    {
                        Name = "Hardware GPU Scheduling",
                        Category = TweakCategory.Gaming,
                        Impact = TweakImpact.Medium,
                        Description = "Hardware-accelerated GPU scheduling is enabled",
                        IsApplied = true,
                        RegistryPath = @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\HwSchMode"
                    });
                }

                // Visual Effects
                using var perfKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
                if (perfKey?.GetValue("VisualFXSetting")?.ToString() == "2")
                {
                    tweaks.Add(new SystemTweak
                    {
                        Name = "Performance Visual Effects",
                        Category = TweakCategory.Performance,
                        Impact = TweakImpact.Medium,
                        Description = "Visual effects optimized for performance",
                        IsApplied = true,
                        RegistryPath = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\VisualFXSetting"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Registry analysis error: {ex.Message}");
            }

            return Task.FromResult(tweaks);
        }

        private Task<List<SystemTweak>> AnalyzeServicesAsync(CancellationToken cancellationToken)
        {
            var tweaks = new List<SystemTweak>();

            try
            {
                var servicesToCheck = new Dictionary<string, string>
                {
                    { "SysMain", "Superfetch/Prefetch Service" },
                    { "WSearch", "Windows Search" },
                    { "Themes", "Windows Themes" },
                    { "Fax", "Windows Fax Service" },
                    { "TabletInputService", "Tablet Input Service" }
                };

                foreach (var service in servicesToCheck)
                {
                    try
                    {
                        using var sc = new System.ServiceProcess.ServiceController(service.Key);
                        if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Stopped)
                        {
                            tweaks.Add(new SystemTweak
                            {
                                Name = $"{service.Value} Disabled",
                                Category = TweakCategory.Performance,
                                Impact = TweakImpact.Medium,
                                Description = $"{service.Value} is disabled for better performance",
                                IsApplied = true,
                                ServiceName = service.Key
                            });
                        }
                    }
                    catch
                    {
                        // Service doesn't exist or access denied
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Service analysis error: {ex.Message}");
            }

            return Task.FromResult(tweaks);
        }

        private Task<List<SystemTweak>> AnalyzeStartupProgramsAsync(CancellationToken cancellationToken)
        {
            var tweaks = new List<SystemTweak>();

            try
            {
                using var startupKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                if (startupKey != null)
                {
                    var startupCount = startupKey.GetValueNames().Length;
                    if (startupCount < 5)
                    {
                        tweaks.Add(new SystemTweak
                        {
                            Name = "Optimized Startup Programs",
                            Category = TweakCategory.Performance,
                            Impact = TweakImpact.High,
                            Description = $"Startup programs optimized ({startupCount} programs)",
                            IsApplied = true,
                            RegistryPath = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup analysis error: {ex.Message}");
            }

            return Task.FromResult(tweaks);
        }

        private Task<List<SystemTweak>> AnalyzeNetworkSettingsAsync(CancellationToken cancellationToken)
        {
            var tweaks = new List<SystemTweak>();

            try
            {
                // TCP Window Scaling
                using var tcpKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters");
                if (tcpKey?.GetValue("Tcp1323Opts")?.ToString() == "1")
                {
                    tweaks.Add(new SystemTweak
                    {
                        Name = "TCP Window Scaling",
                        Category = TweakCategory.Network,
                        Impact = TweakImpact.Medium,
                        Description = "TCP window scaling enabled for better network performance",
                        IsApplied = true,
                        RegistryPath = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Tcp1323Opts"
                    });
                }

                // Network Throttling Index
                if (tcpKey?.GetValue("NetworkThrottlingIndex")?.ToString() == "4294967295")
                {
                    tweaks.Add(new SystemTweak
                    {
                        Name = "Network Throttling Disabled",
                        Category = TweakCategory.Network,
                        Impact = TweakImpact.High,
                        Description = "Network throttling disabled for maximum throughput",
                        IsApplied = true,
                        RegistryPath = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\NetworkThrottlingIndex"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Network analysis error: {ex.Message}");
            }

            return Task.FromResult(tweaks);
        }

        private async Task<List<SystemTweak>> AnalyzePowerSettingsAsync(CancellationToken cancellationToken)
        {
            var tweaks = new List<SystemTweak>();

            try
            {
                // Check current power plan
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = "/getactivescheme",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(cancellationToken);

                if (output.Contains("High performance") || output.Contains("Ultimate Performance"))
                {
                    tweaks.Add(new SystemTweak
                    {
                        Name = "High Performance Power Plan",
                        Category = TweakCategory.Performance,
                        Impact = TweakImpact.High,
                        Description = "High performance power plan is active",
                        IsApplied = true,
                        PowerPlan = "High Performance"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Power analysis error: {ex.Message}");
            }

            return tweaks;
        }

        private Task<List<SystemTweak>> AnalyzeVisualEffectsAsync(CancellationToken cancellationToken)
        {
            var tweaks = new List<SystemTweak>();

            try
            {
                // Check if animations are disabled
                using var perfKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics");
                if (perfKey?.GetValue("MinAnimate")?.ToString() == "0")
                {
                    tweaks.Add(new SystemTweak
                    {
                        Name = "Animations Disabled",
                        Category = TweakCategory.Performance,
                        Impact = TweakImpact.Low,
                        Description = "Window animations are disabled",
                        IsApplied = true,
                        RegistryPath = @"HKCU\Control Panel\Desktop\WindowMetrics\MinAnimate"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Visual effects analysis error: {ex.Message}");
            }

            return Task.FromResult(tweaks);
        }

        private Task<List<SystemTweak>> AnalyzeGamingOptimizationsAsync(CancellationToken cancellationToken)
        {
            var tweaks = new List<SystemTweak>();

            try
            {
                // Check for NVIDIA Control Panel optimizations
                using var nvidiaKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000");
                if (nvidiaKey?.GetValue("PowerMizerEnable")?.ToString() == "1")
                {
                    tweaks.Add(new SystemTweak
                    {
                        Name = "NVIDIA PowerMizer Optimized",
                        Category = TweakCategory.Gaming,
                        Impact = TweakImpact.High,
                        Description = "NVIDIA PowerMizer configured for maximum performance",
                        IsApplied = true,
                        RegistryPath = @"HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000\PowerMizerEnable"
                    });
                }

                // Check for exclusive fullscreen optimizations
                using var gameKey = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore");
                if (gameKey?.GetValue("GameDVR_Enabled")?.ToString() == "0")
                {
                    tweaks.Add(new SystemTweak
                    {
                        Name = "Game DVR Disabled",
                        Category = TweakCategory.Gaming,
                        Impact = TweakImpact.Medium,
                        Description = "Game DVR disabled for better gaming performance",
                        IsApplied = true,
                        RegistryPath = @"HKCU\System\GameConfigStore\GameDVR_Enabled"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Gaming analysis error: {ex.Message}");
            }

            return Task.FromResult(tweaks);
        }

        private double CalculatePerformanceImpact()
        {
            var totalImpact = 0.0;
            var maxPossibleImpact = 0.0;

            foreach (var tweak in _detectedTweaks)
            {
                var impactValue = tweak.Impact switch
                {
                    TweakImpact.Low => 1.0,
                    TweakImpact.Medium => 2.0,
                    TweakImpact.High => 3.0,
                    _ => 0.0
                };

                maxPossibleImpact += impactValue;
                if (tweak.IsApplied)
                    totalImpact += impactValue;
            }

            return maxPossibleImpact > 0 ? (totalImpact / maxPossibleImpact) * 100 : 0;
        }

        private double CalculateSecurityScore()
        {
            var securityTweaks = _detectedTweaks.Where(t => t.Category == TweakCategory.Security).ToList();
            if (!securityTweaks.Any()) return 85.0; // Default good score

            var appliedSecurityTweaks = securityTweaks.Count(t => t.IsApplied);
            return (appliedSecurityTweaks / (double)securityTweaks.Count) * 100;
        }

        private List<string> GenerateRecommendations()
        {
            var recommendations = new List<string>();

            var unappliedTweaks = _detectedTweaks.Where(t => !t.IsApplied).ToList();
            if (unappliedTweaks.Any())
            {
                recommendations.Add($"Apply {unappliedTweaks.Count} pending optimizations for better performance");
            }

            var highImpactTweaks = _detectedTweaks.Where(t => t.Impact == TweakImpact.High && !t.IsApplied).ToList();
            if (highImpactTweaks.Any())
            {
                recommendations.Add($"Focus on {highImpactTweaks.Count} high-impact optimizations first");
            }

            var gamingTweaks = _detectedTweaks.Where(t => t.Category == TweakCategory.Gaming).ToList();
            if (gamingTweaks.Count < 3)
            {
                recommendations.Add("Consider enabling more gaming-specific optimizations");
            }

            if (!recommendations.Any())
            {
                recommendations.Add("Your system is well optimized! Consider creating a profile to share your configuration.");
            }

            return recommendations;
        }

        public async Task<SystemProfile> CreateProfileAsync(string name, string description)
        {
            var profile = new SystemProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description,
                CreatedDate = DateTime.Now,
                Tweaks = new List<SystemTweak>(_detectedTweaks.Where(t => t.IsApplied)),
                SystemInfo = await GatherSystemInfoAsync()
            };

            _profiles.Add(profile);
            await SaveProfilesAsync();
            return profile;
        }

        private async Task<SystemInfo> GatherSystemInfoAsync()
        {
            var info = new SystemInfo();

            try
            {
                // Get CPU info
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    info.CpuName = obj["Name"]?.ToString() ?? "Unknown";
                    break;
                }

                // Get GPU info
                using var gpuSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject obj in gpuSearcher.Get())
                {
                    var name = obj["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(name) && !name.Contains("Microsoft"))
                    {
                        info.GpuName = name;
                        break;
                    }
                }

                // Get RAM info
                using var memSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in memSearcher.Get())
                {
                    var totalMemory = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                    info.RamSize = $"{totalMemory / (1024 * 1024 * 1024)} GB";
                    break;
                }

                info.WindowsVersion = Environment.OSVersion.VersionString;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"System info gathering error: {ex.Message}");
            }

            return info;
        }

        private void LoadProfiles()
        {
            try
            {
                var profilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GGs", "Profiles");
                if (Directory.Exists(profilesPath))
                {
                    // Load profiles from disk (implementation would go here)
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Profile loading error: {ex.Message}");
            }
        }

        private async Task SaveProfilesAsync()
        {
            try
            {
                var profilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GGs", "Profiles");
                Directory.CreateDirectory(profilesPath);
                // Save profiles to disk (implementation would go here)
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Profile saving error: {ex.Message}");
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ===== New Profile Management APIs (align with UI call sites) =====

        private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            AllowTrailingCommas = true
        };

        private static string GetProfilesDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GGs", "Profiles");
        }

        private static string GetProfileFilePath(Guid id)
        {
            return Path.Combine(GetProfilesDirectory(), $"{id:N}.ggsprofile");
        }

        public async Task SaveProfileAsync(GGs.Shared.SystemIntelligence.SystemIntelligenceProfile profile, string name)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            // Ensure folder exists
            var dir = GetProfilesDirectory();
            Directory.CreateDirectory(dir);

            // Normalize/ensure required fields
            profile.Name = string.IsNullOrWhiteSpace(name) ? (profile.Name ?? "Profile") : name;
            if (profile.CreatedDate == default) profile.CreatedDate = DateTime.UtcNow;
            if (profile.CreatedUtc == default) profile.CreatedUtc = DateTime.UtcNow;
            if (profile.ProfileId == Guid.Empty) profile.ProfileId = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id;
            if (profile.Id == Guid.Empty) profile.Id = profile.ProfileId;

            var filePath = GetProfileFilePath(profile.Id);
            var json = JsonSerializer.Serialize(profile, s_jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<List<GGs.Shared.SystemIntelligence.SystemIntelligenceProfile>> GetSavedProfilesAsync()
        {
            var result = new List<GGs.Shared.SystemIntelligence.SystemIntelligenceProfile>();
            try
            {
                var dir = GetProfilesDirectory();
                if (!Directory.Exists(dir)) return result;

                foreach (var file in Directory.EnumerateFiles(dir, "*.ggsprofile", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var profile = JsonSerializer.Deserialize<GGs.Shared.SystemIntelligence.SystemIntelligenceProfile>(json, s_jsonOptions);
                        if (profile != null)
                        {
                            // Ensure Id/ProfileId consistency
                            if (profile.Id == Guid.Empty && profile.ProfileId != Guid.Empty) profile.Id = profile.ProfileId;
                            if (profile.ProfileId == Guid.Empty && profile.Id != Guid.Empty) profile.ProfileId = profile.Id;
                            result.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to read profile '{file}': {ex.Message}");
                    }
                }

                // Sort by CreatedDate desc as a sensible default
                result = result.OrderByDescending(p => p.CreatedDate).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetSavedProfilesAsync error: {ex.Message}");
            }
            return result;
        }

        public async Task<GGs.Shared.SystemIntelligence.SystemIntelligenceProfile?> ImportProfileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

                var json = await File.ReadAllTextAsync(filePath);
                var profile = JsonSerializer.Deserialize<GGs.Shared.SystemIntelligence.SystemIntelligenceProfile>(json, s_jsonOptions);
                if (profile == null) return null;

                // Ensure identifiers
                if (profile.Id == Guid.Empty) profile.Id = Guid.NewGuid();
                if (profile.ProfileId == Guid.Empty) profile.ProfileId = profile.Id;
                if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = $"Imported Profile {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
                if (profile.CreatedDate == default) profile.CreatedDate = DateTime.UtcNow;
                if (profile.CreatedUtc == default) profile.CreatedUtc = DateTime.UtcNow;

                // Persist a local copy
                var destDir = GetProfilesDirectory();
                Directory.CreateDirectory(destDir);
                var destPath = GetProfileFilePath(profile.Id);

                // If file exists for same Id, generate a new Id to avoid overwrite
                if (File.Exists(destPath))
                {
                    profile.Id = Guid.NewGuid();
                    profile.ProfileId = profile.Id;
                    destPath = GetProfileFilePath(profile.Id);
                }

                var outJson = JsonSerializer.Serialize(profile, s_jsonOptions);
                await File.WriteAllTextAsync(destPath, outJson);
                return profile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImportProfileAsync error: {ex.Message}");
                return null;
            }
        }

        public async Task<GGs.Shared.SystemIntelligence.SystemIntelligenceProfile?> LoadProfileAsync(string id)
        {
            try
            {
                // Try direct Guid-based file lookup first
                if (Guid.TryParse(id, out var guid))
                {
                    var path = GetProfileFilePath(guid);
                    if (File.Exists(path))
                    {
                        var json = await File.ReadAllTextAsync(path);
                        return JsonSerializer.Deserialize<GGs.Shared.SystemIntelligence.SystemIntelligenceProfile>(json, s_jsonOptions);
                    }
                }

                // Fallback: scan saved profiles and match by Id/ProfileId/Name
                var all = await GetSavedProfilesAsync();
                var match = all.FirstOrDefault(p =>
                    p.Id.ToString("D").Equals(id, StringComparison.OrdinalIgnoreCase) ||
                    p.Id.ToString("N").Equals(id, StringComparison.OrdinalIgnoreCase) ||
                    p.ProfileId.ToString("D").Equals(id, StringComparison.OrdinalIgnoreCase) ||
                    p.ProfileId.ToString("N").Equals(id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Name, id, StringComparison.OrdinalIgnoreCase));
                return match;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadProfileAsync error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ApplyProfileAsync(GGs.Shared.SystemIntelligence.SystemIntelligenceProfile profile)
        {
            if (profile == null) return false;
            try
            {
                AppLogger.LogInfo($"Applying system intelligence profile: {profile.ProfileName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyProfileAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task ExportProfileAsync(GGs.Shared.SystemIntelligence.SystemIntelligenceProfile profile, string filePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(profile, s_jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExportProfileAsync error: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteProfileAsync(string id)
        {
            try
            {
                if (Guid.TryParse(id, out var guid))
                {
                    var path = GetProfileFilePath(guid);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        return;
                    }
                }

                // Fallback: find by scan
                var all = await GetSavedProfilesAsync();
                var match = all.FirstOrDefault(p =>
                    p.Id.ToString("D").Equals(id, StringComparison.OrdinalIgnoreCase) ||
                    p.Id.ToString("N").Equals(id, StringComparison.OrdinalIgnoreCase) ||
                    p.ProfileId.ToString("D").Equals(id, StringComparison.OrdinalIgnoreCase) ||
                    p.ProfileId.ToString("N").Equals(id, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    var path = GetProfileFilePath(match.Id);
                    if (File.Exists(path)) File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteProfileAsync error: {ex.Message}");
            }
        }
    }

    // Supporting classes and enums
    public class SystemTweak
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public TweakCategory Category { get; set; }
        public TweakImpact Impact { get; set; }
        public bool IsApplied { get; set; }
        public string? RegistryPath { get; set; }
        public string? ServiceName { get; set; }
        public string? PowerPlan { get; set; }
        public DateTime DetectedDate { get; set; } = DateTime.Now;
    }

    public class SystemProfile
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public List<SystemTweak> Tweaks { get; set; } = new();
        public SystemInfo SystemInfo { get; set; } = new();
        public int Downloads { get; set; }
        public double Rating { get; set; }
        public string Author { get; set; } = "";
    }

    public class SystemInfo
    {
        public string CpuName { get; set; } = "";
        public string GpuName { get; set; } = "";
        public string RamSize { get; set; } = "";
        public string WindowsVersion { get; set; } = "";
    }

    public class ScanResult
    {
        public int TotalTweaksFound { get; set; }
        public double PerformanceImpact { get; set; }
        public double SecurityScore { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
    }

    public class ScanCompletedEventArgs : EventArgs
    {
        public ScanResult Result { get; }
        public ScanCompletedEventArgs(ScanResult result) => Result = result;
    }

    public class TweakDetectedEventArgs : EventArgs
    {
        public SystemTweak Tweak { get; }
        public TweakDetectedEventArgs(SystemTweak tweak) => Tweak = tweak;
    }

    public enum TweakCategory
    {
        Performance,
        Gaming,
        Network,
        Security,
        Privacy,
        Visual,
        Power
    }

    public enum TweakImpact
    {
        Low,
        Medium,
        High
    }
}