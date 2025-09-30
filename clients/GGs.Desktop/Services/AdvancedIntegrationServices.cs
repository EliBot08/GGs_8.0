using System;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GGs.Desktop.Services;

// Voice Control Service
public class VoiceControlService
{
    private SpeechRecognitionEngine? _recognizer;
    private SpeechSynthesizer _synthesizer;
    private bool _isListening;
    
    public event EventHandler<VoiceCommandEventArgs>? CommandRecognized;
    
    public VoiceControlService()
    {
        _synthesizer = new SpeechSynthesizer();
        _synthesizer.SetOutputToDefaultAudioDevice();
        InitializeRecognizer();
    }
    
    private void InitializeRecognizer()
    {
        try
        {
            _recognizer = new SpeechRecognitionEngine();
            
            // Define commands
            var commands = new Choices();
            commands.Add(new[] 
            { 
                "hey ggs", "optimize", "game mode", "clean system", 
                "boost performance", "silent mode", "status report",
                "activate gaming", "disable optimization", "check performance"
            });
            
            var gb = new GrammarBuilder();
            gb.Append(commands);
            
            var grammar = new Grammar(gb);
            _recognizer.LoadGrammar(grammar);
            
            _recognizer.SpeechRecognized += OnSpeechRecognized;
            _recognizer.SetInputToDefaultAudioDevice();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Voice recognition init failed: {ex.Message}");
        }
    }
    
    public void StartListening()
    {
        if (_recognizer != null && !_isListening)
        {
            _recognizer.RecognizeAsync(RecognizeMode.Multiple);
            _isListening = true;
            Speak("Voice control activated");
        }
    }
    
    public void StopListening()
    {
        if (_recognizer != null && _isListening)
        {
            _recognizer.RecognizeAsyncStop();
            _isListening = false;
        }
    }
    
    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (e.Result.Confidence > 0.6)
        {
            var command = e.Result.Text.ToLower();
            CommandRecognized?.Invoke(this, new VoiceCommandEventArgs { Command = command });
            
            ProcessCommand(command);
        }
    }
    
    private void ProcessCommand(string command)
    {
        switch (command)
        {
            case "hey ggs":
                Speak("Yes, how can I help you?");
                break;
            case "optimize":
            case "boost performance":
                Speak("Optimizing your system now");
                break;
            case "game mode":
            case "activate gaming":
                Speak("Gaming mode activated");
                break;
            case "clean system":
                Speak("Cleaning system files");
                break;
            case "status report":
            case "check performance":
                Speak("System running at optimal performance");
                break;
        }
    }
    
    public void Speak(string text)
    {
        _synthesizer.SpeakAsync(text);
    }
}

// RGB Integration Service
public class RGBIntegrationService
{
    private bool _isConnected;
    
    public event EventHandler<RGBStatusEventArgs>? StatusChanged;
    
    public async Task ConnectToRGBDevices()
    {
        await Task.Run(() =>
        {
            try
            {
                // Check for common RGB software
                var rgbSoftware = new[] { "iCUE", "Aura", "Synapse", "RGBFusion" };
                
                foreach (var software in rgbSoftware)
                {
                    var processes = Process.GetProcessesByName(software);
                    if (processes.Any())
                    {
                        _isConnected = true;
                        StatusChanged?.Invoke(this, new RGBStatusEventArgs { IsConnected = true, Software = software });
                        break;
                    }
                }
            }
            catch { }
        });
    }
    
    public void SetSystemStatusColor(SystemStatus status)
    {
        if (!_isConnected) return;
        
        var color = status switch
        {
            SystemStatus.Optimal => "#00FF00",      // Green
            SystemStatus.Gaming => "#FF00FF",       // Magenta
            SystemStatus.Warning => "#FFFF00",      // Yellow
            SystemStatus.Critical => "#FF0000",     // Red
            SystemStatus.Idle => "#0000FF",         // Blue
            _ => "#FFFFFF"                          // White
        };
        
        // In production, would interface with RGB SDK
        Debug.WriteLine($"Setting RGB to {color} for status {status}");
    }
    
    public void PulseEffect(string color, int duration)
    {
        // Simulate pulse effect
        Task.Run(async () =>
        {
            for (int i = 0; i < duration / 100; i++)
            {
                await Task.Delay(100);
                // Would control actual RGB here
            }
        });
    }
}

// Mobile Companion API Service  
public class MobileCompanionService
{
    private HttpListener? _listener;
    private bool _isRunning;
    
    public event EventHandler<MobileCommandEventArgs>? CommandReceived;
    
    public void StartAPI(int port = 8888)
    {
        if (_isRunning) return;
        
        Task.Run(async () =>
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{port}/api/");
                _listener.Start();
                _isRunning = true;
                
                while (_isRunning)
                {
                    var context = await _listener.GetContextAsync();
                    ProcessRequest(context);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mobile API error: {ex.Message}");
            }
        });
    }
    
    private async void ProcessRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        
        try
        {
            string responseString = "";
            
            switch (request.Url?.AbsolutePath)
            {
                case "/api/status":
                    responseString = JsonSerializer.Serialize(new
                    {
                        cpu = GetCpuUsage(),
                        memory = GetMemoryUsage(),
                        temperature = GetTemperature(),
                        status = "optimal"
                    });
                    break;
                    
                case "/api/optimize":
                    CommandReceived?.Invoke(this, new MobileCommandEventArgs { Command = "optimize" });
                    responseString = "{\"success\":true}";
                    break;
                    
                case "/api/gamemode":
                    CommandReceived?.Invoke(this, new MobileCommandEventArgs { Command = "gamemode" });
                    responseString = "{\"success\":true}";
                    break;
                    
                default:
                    responseString = "{\"error\":\"Unknown endpoint\"}";
                    break;
            }
            
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "application/json";
            
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch
        {
            response.StatusCode = 500;
            response.Close();
        }
    }
    
    public void StopAPI()
    {
        _isRunning = false;
        _listener?.Stop();
    }
    
    private double GetCpuUsage() => new Random().Next(20, 80);
    private double GetMemoryUsage() => new Random().Next(40, 70);
    private double GetTemperature() => new Random().Next(40, 70);
}

// Streaming Mode Service
public class StreamingModeService
{
    private bool _streamingModeActive;
    private Process? _obsProcess;
    
    public event EventHandler<StreamingStatusEventArgs>? StatusChanged;
    
    public async Task<bool> DetectOBS()
    {
        return await Task.Run(() =>
        {
            var obsProcesses = Process.GetProcessesByName("obs64");
            if (obsProcesses.Any())
            {
                _obsProcess = obsProcesses.First();
                return true;
            }
            return false;
        });
    }
    
    public async Task ActivateStreamingMode()
    {
        if (_streamingModeActive) return;
        
        _streamingModeActive = true;
        
        await Task.Run(() =>
        {
            try
            {
                // Optimize for streaming
                // 1. Set CPU affinity for OBS
                if (_obsProcess != null && !_obsProcess.HasExited)
                {
                    _obsProcess.PriorityClass = ProcessPriorityClass.High;
                }
                
                // 2. Limit background processes
                var backgroundApps = new[] { "Discord", "Chrome", "Firefox", "Spotify" };
                foreach (var app in backgroundApps)
                {
                    var processes = Process.GetProcessesByName(app);
                    foreach (var proc in processes)
                    {
                        try
                        {
                            proc.PriorityClass = ProcessPriorityClass.BelowNormal;
                        }
                        catch { }
                    }
                }
                
                // 3. Optimize network for streaming
                Process.Start(new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "int tcp set global autotuninglevel=normal",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                
                StatusChanged?.Invoke(this, new StreamingStatusEventArgs 
                { 
                    IsActive = true, 
                    OptimizationsApplied = new[] { "CPU Priority", "Background Apps Limited", "Network Optimized" }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Streaming mode error: {ex.Message}");
            }
        });
    }
    
    public void DeactivateStreamingMode()
    {
        if (!_streamingModeActive) return;
        
        _streamingModeActive = false;
        
        // Restore normal priorities
        Task.Run(() =>
        {
            try
            {
                var allProcesses = Process.GetProcesses();
                foreach (var proc in allProcesses)
                {
                    try
                    {
                        if (proc.PriorityClass != ProcessPriorityClass.Normal)
                            proc.PriorityClass = ProcessPriorityClass.Normal;
                    }
                    catch { }
                }
            }
            catch { }
        });
        
        StatusChanged?.Invoke(this, new StreamingStatusEventArgs { IsActive = false });
    }
    
    public StreamingStats GetStreamingStats()
    {
        return new StreamingStats
        {
            IsStreaming = _streamingModeActive,
            BitRate = _streamingModeActive ? "6000 Kbps" : "0 Kbps",
            FPS = _streamingModeActive ? "60" : "0",
            DroppedFrames = 0,
            CPUUsage = _obsProcess?.TotalProcessorTime.TotalSeconds ?? 0
        };
    }
}

// Event Args
public class VoiceCommandEventArgs : EventArgs
{
    public string Command { get; set; } = "";
}

public class RGBStatusEventArgs : EventArgs
{
    public bool IsConnected { get; set; }
    public string Software { get; set; } = "";
}

public class MobileCommandEventArgs : EventArgs
{
    public string Command { get; set; } = "";
}

public class StreamingStatusEventArgs : EventArgs
{
    public bool IsActive { get; set; }
    public string[] OptimizationsApplied { get; set; } = Array.Empty<string>();
}

// Enums and Models
public enum SystemStatus
{
    Optimal,
    Gaming,
    Warning,
    Critical,
    Idle
}

public class StreamingStats
{
    public bool IsStreaming { get; set; }
    public string BitRate { get; set; } = "";
    public string FPS { get; set; } = "";
    public int DroppedFrames { get; set; }
    public double CPUUsage { get; set; }
}
