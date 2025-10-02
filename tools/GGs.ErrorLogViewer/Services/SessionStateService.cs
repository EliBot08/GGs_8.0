#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GGs.ErrorLogViewer.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services
{
    public interface ISessionStateService
    {
        SessionState? CurrentState { get; }
        
        void SaveState(SessionState state);
        SessionState? LoadState();
        Task<SessionState?> LoadAsync(string filePath);
        Task SaveAsync(string filePath, SessionState state);
        void ClearState();
        void StartAutoSave(Func<SessionState> stateProvider);
        void StopAutoSave();
    }

    public class SessionStateService : BackgroundService, ISessionStateService
    {
        private readonly ILogger<SessionStateService> _logger;
        private readonly string _stateFilePath;
        private Timer? _autoSaveTimer;
        private Func<SessionState>? _stateProvider;
        private readonly TimeSpan _autoSaveInterval = TimeSpan.FromSeconds(30);

        public SessionState? CurrentState { get; private set; }

        public SessionStateService(ILogger<SessionStateService> logger)
        {
            _logger = logger;
            
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GGs", "ErrorLogViewer");
            
            Directory.CreateDirectory(appDataPath);
            _stateFilePath = Path.Combine(appDataPath, "session_state.json");
        }

        public void SaveState(SessionState state)
        {
            try
            {
                state.SavedAt = DateTime.UtcNow;
                CurrentState = state;
                
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                // Write to temp file first, then move (atomic operation)
                var tempFile = _stateFilePath + ".tmp";
                File.WriteAllText(tempFile, json);
                
                if (File.Exists(_stateFilePath))
                {
                    File.Delete(_stateFilePath);
                }
                
                File.Move(tempFile, _stateFilePath);
                
                _logger.LogDebug("Session state saved to {FilePath}", _stateFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save session state");
            }
        }

        public SessionState? LoadState()
        {
            try
            {
                if (!File.Exists(_stateFilePath))
                {
                    _logger.LogInformation("No session state file found");
                    return null;
                }

                var json = File.ReadAllText(_stateFilePath);
                CurrentState = JsonSerializer.Deserialize<SessionState>(json);
                
                if (CurrentState != null)
                {
                    var age = DateTime.UtcNow - CurrentState.SavedAt;
                    _logger.LogInformation("Loaded session state from {FilePath}, age: {Age}", 
                        _stateFilePath, age);
                    
                    // Don't restore very old sessions (older than 7 days)
                    if (age > TimeSpan.FromDays(7))
                    {
                        _logger.LogWarning("Session state is too old ({Days} days), ignoring", age.TotalDays);
                        return null;
                    }
                }
                
                return CurrentState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load session state");
                return null;
            }
        }

        public async Task<SessionState?> LoadAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogInformation("No session state file found at {Path}", filePath);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var state = JsonSerializer.Deserialize<SessionState>(json);
                
                if (state != null)
                {
                    var age = DateTime.UtcNow - state.SavedAt;
                    _logger.LogInformation("Loaded session state from {FilePath}, age: {Age}", filePath, age);
                    
                    if (age > TimeSpan.FromDays(7))
                    {
                        _logger.LogWarning("Session state is too old ({Days} days), ignoring", age.TotalDays);
                        return null;
                    }
                }
                
                return state;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load session state from {Path}", filePath);
                return null;
            }
        }

        public async Task SaveAsync(string filePath, SessionState state)
        {
            try
            {
                state.SavedAt = DateTime.UtcNow;
                
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var tempFile = filePath + ".tmp";
                await File.WriteAllTextAsync(tempFile, json);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                File.Move(tempFile, filePath);
                
                _logger.LogDebug("Session state saved to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save session state to {Path}", filePath);
            }
        }

        public void ClearState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    File.Delete(_stateFilePath);
                    _logger.LogInformation("Session state cleared");
                }
                
                CurrentState = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear session state");
            }
        }

        public void StartAutoSave(Func<SessionState> stateProvider)
        {
            _stateProvider = stateProvider;
            _autoSaveTimer = new Timer(AutoSaveCallback, null, _autoSaveInterval, _autoSaveInterval);
            _logger.LogInformation("Auto-save started with interval {Interval}", _autoSaveInterval);
        }

        public void StopAutoSave()
        {
            _autoSaveTimer?.Dispose();
            _autoSaveTimer = null;
            _logger.LogInformation("Auto-save stopped");
        }

        private void AutoSaveCallback(object? state)
        {
            try
            {
                if (_stateProvider != null)
                {
                    var sessionState = _stateProvider();
                    SaveState(sessionState);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-save failed");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SessionStateService background service started");
            
            // Just keep running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override void Dispose()
        {
            StopAutoSave();
            base.Dispose();
        }
    }
}
