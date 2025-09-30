using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace GGs.Desktop.Services;

public interface IProcessProvider
{
    Process[] GetProcesses();
}

public sealed class DefaultProcessProvider : IProcessProvider
{
    public Process[] GetProcesses() => Process.GetProcesses();
}

public interface IGameOptimizationActions
{
    Task ApplyNetworkAsync(CancellationToken ct);
    Task RollbackNetworkAsync(CancellationToken ct);
    bool TryBoostPriority(Process gameProc, out ProcessPriorityClass? previous);
    void RestorePriority(Process gameProc, ProcessPriorityClass previous);
    List<(string name, int pid)> CloseApps(IEnumerable<string> procNames);
}

public sealed class DefaultGameOptimizationActions : IGameOptimizationActions
{
    private readonly NetworkOptimizationService _net = new();

    public async Task ApplyNetworkAsync(CancellationToken ct)
    {
        var adapters = _net.GetActiveAdapterNames();
        var profile = new NetworkProfile
        {
            Name = "Low-Latency Gaming",
            Risk = NetRiskLevel.Medium,
            Autotuning = TcpAutotuningLevel.Normal,
            TcpGlobalOptions = new Dictionary<string, string> { { "rss", "enabled" } }
        };
        foreach (var a in adapters) profile.DnsPerAdapter[a] = new[] { "1.1.1.1" };
        var res = await _net.ApplyProfileAsync(profile);
        if (!res.Success) AppLogger.LogWarn($"ApplyNetwork failed: {res.Message}");
    }

    public async Task RollbackNetworkAsync(CancellationToken ct)
    {
        try { await _net.RollbackLastAsync(); } catch { }
    }

    public bool TryBoostPriority(Process gameProc, out ProcessPriorityClass? previous)
    {
        previous = null;
        try
        {
            previous = gameProc.PriorityClass;
            if (previous != ProcessPriorityClass.High && previous != ProcessPriorityClass.RealTime)
                gameProc.PriorityClass = ProcessPriorityClass.High;
            return true;
        }
        catch { return false; }
    }

    public void RestorePriority(Process gameProc, ProcessPriorityClass previous)
    {
        try { if (!gameProc.HasExited) gameProc.PriorityClass = previous; } catch { }
    }

    public List<(string name, int pid)> CloseApps(IEnumerable<string> procNames)
    {
        var result = new List<(string name, int pid)>();
        var set = new HashSet<string>(procNames.Select(x => (x ?? string.Empty).Trim().ToLowerInvariant()));
        string[] critical = { "system", "idle", "wininit.exe", "winlogon.exe", "lsass.exe", "services.exe", "csrss.exe", "smss.exe", "dwm.exe", "explorer.exe" };
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var name = (p.ProcessName + ".exe").ToLowerInvariant();
                if (!set.Contains(name)) continue;
                if (critical.Contains(name)) continue;
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    p.CloseMainWindow();
                    p.WaitForExit(1500);
                }
                else
                {
                    try { p.Kill(entireProcessTree: true); } catch { }
                }
                result.Add((name, p.Id));
            }
            catch { }
        }
        return result;
    }
}

public class GameDetectionService
{
    private Timer _detectionTimer;
    private readonly List<GameProfile> _knownGames;
    private readonly List<string> _runningGames;
    private readonly string _profilesPath;
    private Process? _currentGame;
    private readonly IProcessProvider _procProvider;
    private readonly IGameOptimizationActions _actions;
    private ProcessPriorityClass? _prevPriority;
    private int _lastGamePid;
    private int? _prevGameMode;
    
    public event EventHandler<GameDetectedEventArgs>? GameDetected;
    public event EventHandler<GameClosedEventArgs>? GameClosed;
    
    public GameDetectionService()
        : this(new DefaultProcessProvider(), new DefaultGameOptimizationActions())
    { }

    public GameDetectionService(IProcessProvider procProvider, IGameOptimizationActions actions)
    {
        _procProvider = procProvider;
        _actions = actions;
        _profilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GGs", "GameProfiles");
        Directory.CreateDirectory(_profilesPath);
        
        _knownGames = LoadKnownGames();
        _runningGames = new List<string>();
        
        _detectionTimer = new Timer(3000); // Check every 3 seconds
        _detectionTimer.Elapsed += async (s, e2) => await ScanOnceAsync();
    }
    
    public void Start()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("GGS_GAME_DETECTION_ENABLED"), "false", StringComparison.OrdinalIgnoreCase))
            return;
        _detectionTimer.Start();
        _ = Task.Run(async () => await ScanOnceAsync());
    }
    
    public void Stop()
    {
        _detectionTimer?.Stop();
    }
    
    public async Task ScanOnceAsync()
    {
        try
        {
            var processes = _procProvider.GetProcesses();
            
            foreach (var process in processes)
            {
                try
                {
                    // Check if this is a known game
                    var game = DetectGame(process);
                    if (game != null && !_runningGames.Contains(process.ProcessName))
                    {
                        _runningGames.Add(process.ProcessName);
                        _currentGame = process;
                        
                        // Trigger game detected event
                        GameDetected?.Invoke(this, new GameDetectedEventArgs 
                        { 
                            Game = game, 
                            Process = process 
                        });
                        
                        // Apply game-specific optimizations
                        await ApplyGameOptimizationsAsync(game, process);
                        
                        // Monitor for game exit
                        _ = Task.Run(() => MonitorGameExit(process, game));
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Game detection error: {ex.Message}");
        }
    }
    
    public GameProfile? DetectGameByNames(IEnumerable<string> processNames)
    {
        var set = new HashSet<string>(processNames.Select(n => (n ?? string.Empty).Trim().ToLowerInvariant()));
        return _knownGames.FirstOrDefault(g => set.Contains(g.ProcessName.ToLowerInvariant()) || set.Contains((g.ProcessName + ".exe").ToLowerInvariant()));
    }

    private GameProfile? DetectGame(Process process)
    {
        // First check known games list
        var knownGame = _knownGames.FirstOrDefault(g => 
            g.ProcessName.Equals(process.ProcessName, StringComparison.OrdinalIgnoreCase));
        
        if (knownGame != null) return knownGame;
        
        // Use heuristics to detect games
        try
        {
            // Check if process has a main window (most games do)
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                var fileName = process.MainModule?.FileName;
                if (!string.IsNullOrEmpty(fileName))
                {
                    // Check common game directories
                    if (IsGameDirectory(fileName))
                    {
                        // Create a new profile for this detected game
                        var newGame = new GameProfile
                        {
                            Name = process.ProcessName,
                            ProcessName = process.ProcessName,
                            ExecutablePath = fileName,
                            AutoDetected = true,
                            OptimizationSettings = GetDefaultGameSettings()
                        };
                        
                        // Add to known games and save
                        _knownGames.Add(newGame);
                        SaveGameProfile(newGame);
                        
                        return newGame;
                    }
                }
            }
        }
        catch { }
        
        return null;
    }
    
    private bool IsGameDirectory(string path)
    {
        var gameDirectories = new[]
        {
            @"Steam\steamapps",
            @"Epic Games",
            @"Origin Games",
            @"Ubisoft",
            @"GOG Galaxy",
            @"Riot Games",
            @"Battle.net",
            @"EA Games",
            @"Program Files\Steam",
            @"Program Files (x86)\Steam",
            "Games"
        };
        
        return gameDirectories.Any(dir => path.Contains(dir, StringComparison.OrdinalIgnoreCase));
    }
    
    private async Task ApplyGameOptimizationsAsync(GameProfile game, Process process)
    {
        try
        {
            var settings = game.OptimizationSettings;
            _lastGamePid = process.Id;
            // Priority boost with restore capture
            if (settings.ProcessPriority != ProcessPriorityClass.Normal)
            {
                try { _prevPriority = process.PriorityClass; process.PriorityClass = settings.ProcessPriority; } catch { }
            }
            // Close apps safely
            if (settings.AppsToClose?.Any() == true)
            {
                _actions.CloseApps(settings.AppsToClose.Select(a => (a ?? string.Empty).Trim() + ".exe"));
            }
            // Apply network optimizations via service
            if (settings.OptimizeNetwork)
            {
                try { await _actions.ApplyNetworkAsync(CancellationToken.None); } catch { }
            }
            // High performance power via elevation
            if (settings.HighPerformancePower)
            {
                try { await ElevationService.PowerCfgSetActiveAsync(Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")); } catch { }
            }
            // Disable GameMode but remember previous
            if (settings.DisableWindowsGameMode)
            {
                try
                {
                    var prev = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", null);
                    _prevGameMode = prev is int i ? i : (int?)null;
                    Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", 0);
                }
                catch { }
            }
            Debug.WriteLine($"Applied optimizations for {game.Name}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply game optimizations: {ex.Message}");
        }
    }
    
    private void MonitorGameExit(Process gameProcess, GameProfile game)
    {
        try
        {
            gameProcess.WaitForExit();
            
            // Game has exited
            _runningGames.Remove(gameProcess.ProcessName);
            _currentGame = null;
            
            // Trigger game closed event
            GameClosed?.Invoke(this, new GameClosedEventArgs { Game = game });
            
            // Restore system settings
            RestoreSystemSettings();
        }
        catch { }
    }
    
    private void RestoreSystemSettings()
    {
        Task.Run(async () =>
        {
            try
            {
                // Restore balanced power plan
                try { await ElevationService.PowerCfgSetActiveAsync(Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e")); } catch { }
                // Rollback network
                try { await _actions.RollbackNetworkAsync(CancellationToken.None); } catch { }
                // Restore GameMode
                if (_prevGameMode.HasValue)
                {
                    try { Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", _prevGameMode.Value); } catch { }
                    _prevGameMode = null;
                }
                // Restore process priority
                if (_prevPriority.HasValue && _lastGamePid != 0)
                {
                    try
                    {
                        var p = Process.GetProcessById(_lastGamePid);
                        if (!p.HasExited) p.PriorityClass = _prevPriority.Value;
                    }
                    catch { }
                    _prevPriority = null; _lastGamePid = 0;
                }
                Debug.WriteLine("System settings restored");
            }
            catch { }
        });
    }
    
    private List<GameProfile> LoadKnownGames()
    {
        var games = new List<GameProfile>();
        
        // Load saved profiles
        try
        {
            var profileFiles = Directory.GetFiles(_profilesPath, "*.json");
            foreach (var file in profileFiles)
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<GameProfile>(json);
                if (profile != null)
                    games.Add(profile);
            }
        }
        catch { }
        
        // Add popular games if no profiles exist
        if (games.Count == 0)
        {
            games.AddRange(GetPopularGames());
        }
        
        return games;
    }
    
    private List<GameProfile> GetPopularGames()
    {
        return new List<GameProfile>
        {
            new GameProfile
            {
                Name = "Counter-Strike 2",
                ProcessName = "cs2",
                OptimizationSettings = new GameOptimizationSettings
                {
                    ProcessPriority = ProcessPriorityClass.High,
                    OptimizeNetwork = true,
                    HighPerformancePower = true,
                    AppsToClose = new[] { "Discord", "Spotify", "Chrome" }
                }
            },
            new GameProfile
            {
                Name = "Valorant",
                ProcessName = "VALORANT",
                OptimizationSettings = new GameOptimizationSettings
                {
                    ProcessPriority = ProcessPriorityClass.High,
                    OptimizeNetwork = true,
                    HighPerformancePower = true
                }
            },
            new GameProfile
            {
                Name = "Fortnite",
                ProcessName = "FortniteClient-Win64-Shipping",
                OptimizationSettings = new GameOptimizationSettings
                {
                    ProcessPriority = ProcessPriorityClass.AboveNormal,
                    OptimizeNetwork = true,
                    HighPerformancePower = true
                }
            },
            new GameProfile
            {
                Name = "League of Legends",
                ProcessName = "League of Legends",
                OptimizationSettings = new GameOptimizationSettings
                {
                    ProcessPriority = ProcessPriorityClass.High,
                    OptimizeNetwork = true
                }
            },
            new GameProfile
            {
                Name = "Minecraft",
                ProcessName = "javaw",
                OptimizationSettings = new GameOptimizationSettings
                {
                    ProcessPriority = ProcessPriorityClass.AboveNormal,
                    AllocateMoreRAM = true
                }
            }
        };
    }
    
    private GameOptimizationSettings GetDefaultGameSettings()
    {
        return new GameOptimizationSettings
        {
            ProcessPriority = ProcessPriorityClass.AboveNormal,
            OptimizeNetwork = true,
            HighPerformancePower = true,
            DisableWindowsGameMode = false,
            AllocateMoreRAM = false,
            AppsToClose = new[] { "Chrome", "Firefox", "Spotify" }
        };
    }
    
    private void SaveGameProfile(GameProfile profile)
    {
        try
        {
            var fileName = Path.Combine(_profilesPath, $"{profile.ProcessName}.json");
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(fileName, json);
        }
        catch { }
    }
    
    public void LearnFromUser(string processName, GameOptimizationSettings settings)
    {
        var game = _knownGames.FirstOrDefault(g => g.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
        if (game != null)
        {
            game.OptimizationSettings = settings;
            SaveGameProfile(game);
        }
    }
}

public class GameProfile
{
    public string Name { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string? ExecutablePath { get; set; }
    public bool AutoDetected { get; set; }
    public GameOptimizationSettings OptimizationSettings { get; set; } = new();
    public DateTime LastPlayed { get; set; }
    public TimeSpan TotalPlayTime { get; set; }
}

public class GameOptimizationSettings
{
    public ProcessPriorityClass ProcessPriority { get; set; } = ProcessPriorityClass.Normal;
    public bool OptimizeNetwork { get; set; }
    public bool HighPerformancePower { get; set; }
    public bool DisableWindowsGameMode { get; set; }
    public bool AllocateMoreRAM { get; set; }
    public string[]? AppsToClose { get; set; }
}

public class GameDetectedEventArgs : EventArgs
{
    public GameProfile Game { get; set; } = null!;
    public Process Process { get; set; } = null!;
}

public class GameClosedEventArgs : EventArgs
{
    public GameProfile Game { get; set; } = null!;
}
