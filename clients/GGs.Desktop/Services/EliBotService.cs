using GGs.Shared.Enums;
using GGs.Shared.Tweaks;
using GGs.Shared.Api;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GGs.Desktop.Services;

public sealed class EliBotService
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;
    private readonly ILogger<EliBotService> _logger;
    private readonly Dictionary<string, List<TweakDefinition>> _optimizationBundles;
    private readonly Dictionary<string, string> _tweakExplanations;
    private readonly List<EliBotMessage> _conversationHistory;
    
    public EliBotService(HttpClient httpClient, AuthService authService, ILogger<EliBotService> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
        _optimizationBundles = new Dictionary<string, List<TweakDefinition>>();
        _tweakExplanations = new Dictionary<string, string>();
        _conversationHistory = new List<EliBotMessage>();
        InitializeKnowledgeBase();
    }
    
    public async Task<EliBotResponse> AskQuestionAsync(string question)
    {
        try
        {
            // Add user message to conversation history
            _conversationHistory.Add(new EliBotMessage
            {
                Role = "user",
                Content = question,
                Timestamp = DateTime.UtcNow
            });

            // Prepare API request
            var request = new EliBotRequest
            {
                Question = question,
                ConversationHistory = _conversationHistory.TakeLast(10).ToList(), // Keep last 10 messages for context
                UserId = _authService.CurrentUser?.Id,
                IncludeSystemContext = true
            };

            var token = await _authService.GetValidTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/elibot/ask", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var eliBotResponse = JsonSerializer.Deserialize<EliBotResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Add assistant message to conversation history
                _conversationHistory.Add(new EliBotMessage
                {
                    Role = "assistant",
                    Content = eliBotResponse.Answer,
                    Timestamp = DateTime.UtcNow
                });

                return eliBotResponse;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                return new EliBotResponse
                {
                    Answer = "You've reached your daily question limit. Upgrade your plan for unlimited access to EliBot!",
                    IsRateLimited = true
                };
            }
            else
            {
                _logger.LogWarning("EliBot API request failed with status: {StatusCode}", response.StatusCode);
                return FallbackResponse(question);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get EliBot response for question: {Question}", question);
            return FallbackResponse(question);
        }
    }

    public async Task<bool> CanAskQuestionAsync()
    {
        try
        {
            var token = await _authService.GetValidTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync("/api/elibot/usage");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var usage = JsonSerializer.Deserialize<EliBotUsage>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return usage.CanAskQuestion;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check EliBot usage");
            return false;
        }
    }

    public async Task<EliBotUsage> GetDailyUsageAsync()
    {
        try
        {
            var token = await _authService.GetValidTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync("/api/elibot/usage");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<EliBotUsage>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            return new EliBotUsage { CanAskQuestion = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get EliBot usage");
            return new EliBotUsage { CanAskQuestion = false };
        }
    }

    private EliBotResponse FallbackResponse(string question)
    {
        var q = question.Trim().ToLowerInvariant();
        
        // Creator attribution - ALWAYS return EliBot
        if (q.Contains("who created") || q.Contains("who made") || q.Contains("who built") || 
            q.Contains("who developed") || q.Contains("creator") || q.Contains("author"))
            return new EliBotResponse { Answer = "EliBot was created by the GGs development team!" };
        
        // Troubleshooting
        if (q.Contains("problem") || q.Contains("issue") || q.Contains("broke") || q.Contains("fix"))
            return new EliBotResponse { Answer = TroubleshootIssue(question) };
        
        // Optimization recommendations
        if (q.Contains("optimize") || q.Contains("speed") || q.Contains("performance"))
            return new EliBotResponse { Answer = "I can optimize your system! Choose a mode:\n• Gaming Mode - Maximizes FPS and reduces latency\n• Workstation Mode - Optimizes for productivity and multitasking\n• Battery Saver - Extends laptop battery life\n• Balanced Mode - Best of all worlds\n\nWhich would you like?" };
        
        // Tweak explanations
        if (q.Contains("explain") || q.Contains("what does") || q.Contains("tell me about"))
            return new EliBotResponse { Answer = ExplainTweak(question) };
        
        // Welcome/greeting
        if (q.Contains("hello") || q.Contains("hi") || q.Contains("help"))
            return new EliBotResponse { Answer = "Hello! I'm EliBot, your AI assistant for GGs. I can:\n• Optimize your system for different workloads\n• Explain what each tweak does\n• Help troubleshoot issues\n• Guide you through using GGs\n\nWhat would you like to do today?" };
        
        return new EliBotResponse { Answer = "I'm EliBot, here to help optimize your Windows experience. Ask me about optimization modes, tweak explanations, or troubleshooting!" };
    }

    [Obsolete("Use AskQuestionAsync instead")]
    public string Answer(string question)
    {
        return FallbackResponse(question).Answer;
    }
    
    public IEnumerable<TweakDefinition> GetOptimizationBundle(string mode)
    {
        var key = mode.ToLowerInvariant();
        if (_optimizationBundles.TryGetValue(key, out var bundle))
            return bundle;
        return Array.Empty<TweakDefinition>();
    }
    
    public string ExplainTweak(string tweakNameOrQuestion)
    {
        // Extract tweak name from question
        var words = tweakNameOrQuestion.ToLowerInvariant().Split(' ');
        
        foreach (var explanation in _tweakExplanations)
        {
            if (words.Any(w => explanation.Key.Contains(w)))
                return explanation.Value;
        }
        
        return "This tweak modifies system settings to improve performance. Each tweak is carefully tested to ensure system stability. Would you like more details about a specific tweak?";
    }
    
    public string TroubleshootIssue(string issue)
    {
        var i = issue.ToLowerInvariant();
        
        if (i.Contains("slow") || i.Contains("performance"))
            return "Performance issues after tweaking? Try these steps:\n1. Create a system restore point before applying tweaks\n2. Apply tweaks one at a time to identify problematic ones\n3. Use the Undo feature to revert recent changes\n4. Run 'sfc /scannow' in admin command prompt\n5. Check Event Viewer for errors\n\nNeed help with a specific issue?";
        
        if (i.Contains("boot") || i.Contains("startup"))
            return "Boot issues can often be resolved by:\n1. Boot into Safe Mode (F8 during startup)\n2. Use System Restore to revert to before tweaks\n3. Run 'bcdedit /deletevalue {current} safeboot' if stuck in safe mode\n4. Use Windows Recovery Environment\n\nAlways create restore points before applying system tweaks!";
        
        if (i.Contains("network") || i.Contains("internet"))
            return "Network issues after tweaking? Try:\n1. Run 'netsh winsock reset' as admin\n2. Reset TCP/IP: 'netsh int ip reset'\n3. Flush DNS: 'ipconfig /flushdns'\n4. Check if Windows Firewall was modified\n5. Verify network services are running\n\nUse the Undo feature if a recent tweak caused this.";
        
        return "I can help troubleshoot! For any issues after applying tweaks:\n1. Use the Undo feature to revert recent changes\n2. Check the audit log to see what was modified\n3. Use System Restore if available\n4. Describe your specific issue for targeted help\n\nWhat problem are you experiencing?";
    }
    
    public string GetOnboardingMessage()
    {
        return "🎉 Welcome to GGs!\n\nI'm EliBot, your personal Windows optimization assistant. Let me guide you through getting started:\n\n" +
               "1. **License Activation** ✓ You're all set with your license!\n" +
               "2. **Choose Your Experience**: Your tier unlocks different features and visual themes\n" +
               "3. **Safety First**: I'll create restore points before major changes\n" +
               "4. **Optimization Modes**: Tell me your use case, and I'll recommend the perfect tweaks\n\n" +
               "Ready to supercharge your Windows experience? Let's start with: What's your primary use case - Gaming, Productivity, or General use?";
    }
    
    private void InitializeKnowledgeBase()
    {
        // Gaming Mode Bundle
        _optimizationBundles["gaming"] = new List<TweakDefinition>
        {
            new() { Name = "Disable Xbox Game Bar", CommandType = CommandType.Registry, RegistryPath = "HKCU\\Software\\Microsoft\\GameBar", RegistryValueName = "ShowStartupPanel", RegistryValueType = "DWord", RegistryValueData = "0", Safety = SafetyLevel.Low, Risk = RiskLevel.Low, Category = "Gaming" },
            new() { Name = "Disable Game DVR", CommandType = CommandType.Registry, RegistryPath = "HKCU\\System\\GameConfigStore", RegistryValueName = "GameDVR_Enabled", RegistryValueType = "DWord", RegistryValueData = "0", Safety = SafetyLevel.Low, Risk = RiskLevel.Low, Category = "Gaming" },
            new() { Name = "GPU Hardware Scheduling", CommandType = CommandType.Registry, RegistryPath = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers", RegistryValueName = "HwSchMode", RegistryValueType = "DWord", RegistryValueData = "2", Safety = SafetyLevel.Medium, Risk = RiskLevel.Medium, Category = "Gaming" },
            new() { Name = "Disable Fullscreen Optimizations", CommandType = CommandType.Registry, RegistryPath = "HKCU\\System\\GameConfigStore", RegistryValueName = "GameDVR_FSEBehaviorMode", RegistryValueType = "DWord", RegistryValueData = "2", Safety = SafetyLevel.Low, Risk = RiskLevel.Low, Category = "Gaming" }
        };

        // Safe Baseline Bundle (low-risk)
        _optimizationBundles["baseline"] = new List<TweakDefinition>
        {
            new() { Name = "Disable Xbox Game Bar", CommandType = CommandType.Registry, RegistryPath = "HKCU\\Software\\Microsoft\\GameBar", RegistryValueName = "ShowStartupPanel", RegistryValueType = "DWord", RegistryValueData = "0", Safety = SafetyLevel.Low, Risk = RiskLevel.Low, Category = "Baseline" },
            new() { Name = "Disable Game DVR", CommandType = CommandType.Registry, RegistryPath = "HKCU\\System\\GameConfigStore", RegistryValueName = "GameDVR_Enabled", RegistryValueType = "DWord", RegistryValueData = "0", Safety = SafetyLevel.Low, Risk = RiskLevel.Low, Category = "Baseline" }
        };
        
        // Workstation Mode Bundle
        _optimizationBundles["workstation"] = new List<TweakDefinition>
        {
            new() { Name = "Processor Scheduling for Background Services", CommandType = CommandType.Registry, RegistryPath = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl", RegistryValueName = "Win32PrioritySeparation", RegistryValueType = "DWord", RegistryValueData = "26", Safety = SafetyLevel.Medium, Risk = RiskLevel.Low, Category = "Performance" },
            new() { Name = "Disable Search Indexing", CommandType = CommandType.Service, ServiceName = "WSearch", ServiceAction = ServiceAction.Disable, Safety = SafetyLevel.Medium, Risk = RiskLevel.Medium, Category = "Performance" },
            new() { Name = "Increase File System Memory Cache", CommandType = CommandType.Registry, RegistryPath = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management", RegistryValueName = "LargeSystemCache", RegistryValueType = "DWord", RegistryValueData = "1", Safety = SafetyLevel.Medium, Risk = RiskLevel.Medium, Category = "Performance" }
        };
        
        // Battery Saver Bundle
        _optimizationBundles["battery"] = new List<TweakDefinition>
        {
            new() { Name = "Disable Background Apps", CommandType = CommandType.Registry, RegistryPath = "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications", RegistryValueName = "GlobalUserDisabled", RegistryValueType = "DWord", RegistryValueData = "1", Safety = SafetyLevel.Low, Risk = RiskLevel.Low, Category = "Power" },
            new() { Name = "Disable Cortana", CommandType = CommandType.Registry, RegistryPath = "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search", RegistryValueName = "AllowCortana", RegistryValueType = "DWord", RegistryValueData = "0", Safety = SafetyLevel.Low, Risk = RiskLevel.Low, Category = "Power" }
        };
        
        // Tweak explanations
        _tweakExplanations["game bar"] = "Xbox Game Bar is a Windows overlay for gaming. Disabling it can free up resources and improve FPS, especially on lower-end systems. Safe to disable if you don't use its recording or social features.";
        _tweakExplanations["telemetry"] = "Windows Telemetry collects usage data for Microsoft. Disabling it improves privacy and reduces background network activity. This won't affect Windows Updates or security.";
        _tweakExplanations["cortana"] = "Cortana is Windows' voice assistant. Disabling it saves memory and CPU, especially beneficial for battery life. You can still use Windows Search without Cortana.";
        _tweakExplanations["indexing"] = "Search Indexing pre-processes files for faster searching. Disabling it saves disk I/O and CPU but makes file searches slower. Recommended for SSDs and power users who know their file locations.";
    }
}
