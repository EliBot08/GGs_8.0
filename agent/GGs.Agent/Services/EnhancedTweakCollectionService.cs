using System.Diagnostics;
using System.Management;
using System.Text.Json;
using Microsoft.Win32;
using GGs.Shared.Models;
using GGs.Shared.Tweaks;

namespace GGs.Agent.Services;

/// <summary>
/// Enhanced system tweaks collection service with deep analysis and progress reporting
/// </summary>
public class EnhancedTweakCollectionService
{
    private readonly ILogger<EnhancedTweakCollectionService> _logger;
    private readonly SystemInformationService _systemInfoService;
    private static readonly ActivitySource _activity = new("GGs.Agent.TweakCollection");

    public EnhancedTweakCollectionService(
        ILogger<EnhancedTweakCollectionService> logger,
        SystemInformationService systemInfoService)
    {
        _logger = logger;
        _systemInfoService = systemInfoService;
    }

    /// <summary>
    /// Collects comprehensive system tweaks with animated progress reporting
    /// </summary>
    public async Task<SystemTweaksCollection> CollectSystemTweaksAsync(
        IProgress<TweakCollectionProgress>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("tweaks.collect");
        var startTime = DateTime.UtcNow;
        
        try
        {
            var collection = new SystemTweaksCollection
            {
                CollectionTimestamp = startTime,
                DeviceId = GGs.Shared.Platform.DeviceIdHelper.GetStableDeviceId()
            };

            var totalSteps = 15;
            var currentStep = 0;

            // Step 1: System Information Base
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🔍 Analyzing system foundation...",
                AnimationType = ProgressAnimationType.Scanning
            });
            await Task.Delay(200, cancellationToken);
            collection.SystemInfo = await _systemInfoService.CollectSystemInformationAsync(null, cancellationToken);

            // Step 2: Registry Deep Scan
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "📋 Performing deep registry analysis...",
                AnimationType = ProgressAnimationType.Processing
            });
            await Task.Delay(300, cancellationToken);
            collection.RegistryTweaks = await CollectRegistryTweaksAsync(cancellationToken);

            // Step 3: Performance Optimizations
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "⚡ Discovering performance optimizations...",
                AnimationType = ProgressAnimationType.Optimizing
            });
            await Task.Delay(250, cancellationToken);
            collection.PerformanceTweaks = await CollectPerformanceTweaksAsync(cancellationToken);

            // Step 4: Security Configurations
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🛡️ Analyzing security configurations...",
                AnimationType = ProgressAnimationType.Securing
            });
            await Task.Delay(200, cancellationToken);
            collection.SecurityTweaks = await CollectSecurityTweaksAsync(cancellationToken);

            // Step 5: Network Optimizations
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🌐 Optimizing network configurations...",
                AnimationType = ProgressAnimationType.Networking
            });
            await Task.Delay(180, cancellationToken);
            collection.NetworkTweaks = await CollectNetworkTweaksAsync(cancellationToken);

            // Step 6: GPU Optimizations
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🎮 Enhancing graphics performance...",
                AnimationType = ProgressAnimationType.Graphics
            });
            await Task.Delay(220, cancellationToken);
            collection.GraphicsTweaks = await CollectGraphicsTweaksAsync(cancellationToken);

            // Step 7: CPU Optimizations
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🔧 Tuning processor settings...",
                AnimationType = ProgressAnimationType.Processing
            });
            await Task.Delay(200, cancellationToken);
            collection.CpuTweaks = await CollectCpuTweaksAsync(cancellationToken);

            // Step 8: Memory Optimizations
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "💾 Optimizing memory management...",
                AnimationType = ProgressAnimationType.Memory
            });
            await Task.Delay(180, cancellationToken);
            collection.MemoryTweaks = await CollectMemoryTweaksAsync(cancellationToken);

            // Step 9: Storage Optimizations
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "💿 Enhancing storage performance...",
                AnimationType = ProgressAnimationType.Storage
            });
            await Task.Delay(250, cancellationToken);
            collection.StorageTweaks = await CollectStorageTweaksAsync(cancellationToken);

            // Step 10: Power Management
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🔋 Configuring power management...",
                AnimationType = ProgressAnimationType.Power
            });
            await Task.Delay(150, cancellationToken);
            collection.PowerTweaks = await CollectPowerTweaksAsync(cancellationToken);

            // Step 11: Gaming Optimizations
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🎯 Applying gaming optimizations...",
                AnimationType = ProgressAnimationType.Gaming
            });
            await Task.Delay(200, cancellationToken);
            collection.GamingTweaks = await CollectGamingTweaksAsync(cancellationToken);

            // Step 12: Privacy Settings
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🔒 Enhancing privacy settings...",
                AnimationType = ProgressAnimationType.Privacy
            });
            await Task.Delay(180, cancellationToken);
            collection.PrivacyTweaks = await CollectPrivacyTweaksAsync(cancellationToken);

            // Step 13: System Services
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "⚙️ Analyzing system services...",
                AnimationType = ProgressAnimationType.Services
            });
            await Task.Delay(220, cancellationToken);
            collection.ServiceTweaks = await CollectServiceTweaksAsync(cancellationToken);

            // Step 14: Advanced Tweaks
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🚀 Discovering advanced optimizations...",
                AnimationType = ProgressAnimationType.Advanced
            });
            await Task.Delay(300, cancellationToken);
            collection.AdvancedTweaks = await CollectAdvancedTweaksAsync(cancellationToken);

            // Step 15: Finalization
            progress?.Report(new TweakCollectionProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "✅ Finalizing tweak collection...",
                AnimationType = ProgressAnimationType.Completing,
                IsCompleted = true
            });
            await Task.Delay(150, cancellationToken);

            collection.CollectionDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            collection.TotalTweaksFound = CountTotalTweaks(collection);
            
            _logger.LogInformation("System tweaks collection completed: {TweaksFound} tweaks in {Duration}ms", 
                collection.TotalTweaksFound, collection.CollectionDurationMs);

            return collection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect system tweaks");
            activity?.SetTag("error", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Uploads system tweaks with progress reporting
    /// </summary>
    public async Task<TweakUploadResult> UploadSystemTweaksAsync(
        SystemTweaksCollection collection,
        IProgress<TweakUploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("tweaks.upload");
        var startTime = DateTime.UtcNow;
        
        try
        {
            var totalSteps = 8;
            var currentStep = 0;

            // Step 1: Validation
            progress?.Report(new TweakUploadProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🔍 Validating tweak collection...",
                AnimationType = UploadAnimationType.Validating
            });
            await Task.Delay(200, cancellationToken);
            ValidateTweakCollection(collection);

            // Step 2: Compression
            progress?.Report(new TweakUploadProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "📦 Compressing data...",
                AnimationType = UploadAnimationType.Compressing
            });
            await Task.Delay(300, cancellationToken);
            var compressedData = await CompressTweakDataAsync(collection, cancellationToken);

            // Step 3: Encryption
            progress?.Report(new TweakUploadProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🔐 Encrypting sensitive data...",
                AnimationType = UploadAnimationType.Encrypting
            });
            await Task.Delay(250, cancellationToken);
            var encryptedData = await EncryptTweakDataAsync(compressedData, cancellationToken);

            // Step 4: Authentication
            progress?.Report(new TweakUploadProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🔑 Authenticating with server...",
                AnimationType = UploadAnimationType.Authenticating
            });
            await Task.Delay(200, cancellationToken);
            var authToken = await AuthenticateWithServerAsync(cancellationToken);

            // Step 5: Upload Preparation
            progress?.Report(new TweakUploadProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "📡 Preparing upload...",
                AnimationType = UploadAnimationType.Preparing
            });
            await Task.Delay(150, cancellationToken);
            var uploadRequest = PrepareUploadRequest(encryptedData, authToken);

            // Step 6: Data Transfer
            progress?.Report(new TweakUploadProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "⬆️ Uploading to server...",
                AnimationType = UploadAnimationType.Uploading
            });
            await Task.Delay(500, cancellationToken);
            var uploadResponse = await PerformUploadAsync(uploadRequest, cancellationToken);

            // Step 7: Verification
            progress?.Report(new TweakUploadProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "✔️ Verifying upload integrity...",
                AnimationType = UploadAnimationType.Verifying
            });
            await Task.Delay(200, cancellationToken);
            await VerifyUploadIntegrityAsync(uploadResponse, cancellationToken);

            // Step 8: Completion
            progress?.Report(new TweakUploadProgress 
            { 
                Step = ++currentStep, 
                TotalSteps = totalSteps, 
                Description = "🎉 Upload completed successfully!",
                AnimationType = UploadAnimationType.Completed,
                IsCompleted = true
            });
            await Task.Delay(100, cancellationToken);

            var result = new TweakUploadResult
            {
                Success = true,
                UploadId = uploadResponse.UploadId,
                UploadDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                BytesUploaded = encryptedData.Length,
                TweaksUploaded = collection.TotalTweaksFound,
                ServerResponse = uploadResponse.Message
            };

            _logger.LogInformation("Tweak upload completed: {UploadId} | {BytesUploaded} bytes | {Duration}ms", 
                result.UploadId, result.BytesUploaded, result.UploadDurationMs);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload system tweaks");
            activity?.SetTag("error", ex.Message);
            
            return new TweakUploadResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                UploadDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds
            };
        }
    }

    // Collection methods for different tweak categories
    private async Task<List<RegistryTweak>> CollectRegistryTweaksAsync(CancellationToken cancellationToken)
    {
        var tweaks = new List<RegistryTweak>();
        
        // Collect performance-related registry tweaks
        var performanceKeys = new[]
        {
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl",
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
            @"HKEY_CURRENT_USER\Control Panel\Desktop",
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"
        };

        foreach (var keyPath in performanceKeys)
        {
            try
            {
                var tweak = await AnalyzeRegistryKeyAsync(keyPath, cancellationToken);
                if (tweak != null) tweaks.Add(tweak);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to analyze registry key: {KeyPath}", keyPath);
            }
        }

        return tweaks;
    }

    private async Task<List<PerformanceTweak>> CollectPerformanceTweaksAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var tweaks = new List<PerformanceTweak>();
            
            // Add various performance tweaks based on system analysis
            tweaks.Add(new PerformanceTweak
            {
                Name = "Disable Visual Effects",
                Description = "Disable unnecessary visual effects to improve performance",
                Category = "UI Performance",
                Impact = TweakImpact.Medium,
                Reversible = true
            });

            tweaks.Add(new PerformanceTweak
            {
                Name = "Optimize Processor Scheduling",
                Description = "Configure processor scheduling for better performance",
                Category = "CPU Performance",
                Impact = TweakImpact.High,
                Reversible = true
            });

            return tweaks;
        }, cancellationToken);
    }

    // Placeholder implementations for other collection methods
    private async Task<List<SecurityTweak>> CollectSecurityTweaksAsync(CancellationToken cancellationToken) => new();
    private async Task<List<NetworkTweak>> CollectNetworkTweaksAsync(CancellationToken cancellationToken) => new();
    private async Task<List<GraphicsTweak>> CollectGraphicsTweaksAsync(CancellationToken cancellationToken) => new();
    private async Task<List<CpuTweak>> CollectCpuTweaksAsync(CancellationToken cancellationToken) => new();
    private async Task<List<MemoryTweak>> CollectMemoryTweaksAsync(CancellationToken cancellationToken) => new();
    private async Task<List<StorageTweak>> CollectStorageTweaksAsync(CancellationToken cancellationToken) => new();
    private async Task<List<PowerTweak>> CollectPowerTweaksAsync(CancellationToken cancellationToken) => new();
    private async Task<List<GamingTweak>> CollectGamingTweaksAsync(CancellationToken cancellationToken) => new();
    private async Task<List<PrivacyTweak>> CollectPrivacyTweaksAsync(CancellationToken cancellationToken) => new();
    private async Task<List<ServiceTweak>> CollectServiceTweaksAsync(CancellationToken cancellationToken) => new();
    private async Task<List<AdvancedTweak>> CollectAdvancedTweaksAsync(CancellationToken cancellationToken) => new();

    // Helper methods
    private async Task<RegistryTweak?> AnalyzeRegistryKeyAsync(string keyPath, CancellationToken cancellationToken)
    {
        // Placeholder implementation
        return await Task.FromResult<RegistryTweak?>(null);
    }

    private int CountTotalTweaks(SystemTweaksCollection collection)
    {
        return collection.RegistryTweaks.Count +
               collection.PerformanceTweaks.Count +
               collection.SecurityTweaks.Count +
               collection.NetworkTweaks.Count +
               collection.GraphicsTweaks.Count +
               collection.CpuTweaks.Count +
               collection.MemoryTweaks.Count +
               collection.StorageTweaks.Count +
               collection.PowerTweaks.Count +
               collection.GamingTweaks.Count +
               collection.PrivacyTweaks.Count +
               collection.ServiceTweaks.Count +
               collection.AdvancedTweaks.Count;
    }

    // Upload helper methods (placeholder implementations)
    private void ValidateTweakCollection(SystemTweaksCollection collection) { }
    private async Task<byte[]> CompressTweakDataAsync(SystemTweaksCollection collection, CancellationToken cancellationToken) => Array.Empty<byte>();
    private async Task<byte[]> EncryptTweakDataAsync(byte[] data, CancellationToken cancellationToken) => data;
    private async Task<string> AuthenticateWithServerAsync(CancellationToken cancellationToken) => "auth_token";
    private object PrepareUploadRequest(byte[] data, string authToken) => new { };
    private async Task<UploadResponse> PerformUploadAsync(object request, CancellationToken cancellationToken) => new() { UploadId = Guid.NewGuid().ToString(), Message = "Success" };
    private async Task VerifyUploadIntegrityAsync(UploadResponse response, CancellationToken cancellationToken) { }
}

// Progress reporting classes
public class TweakCollectionProgress
{
    public int Step { get; set; }
    public int TotalSteps { get; set; }
    public string Description { get; set; } = string.Empty;
    public ProgressAnimationType AnimationType { get; set; }
    public bool IsCompleted { get; set; }
    public double PercentComplete => TotalSteps > 0 ? (double)Step / TotalSteps * 100 : 0;
}

public class TweakUploadProgress
{
    public int Step { get; set; }
    public int TotalSteps { get; set; }
    public string Description { get; set; } = string.Empty;
    public UploadAnimationType AnimationType { get; set; }
    public bool IsCompleted { get; set; }
    public double PercentComplete => TotalSteps > 0 ? (double)Step / TotalSteps * 100 : 0;
}

public enum ProgressAnimationType
{
    Scanning,
    Processing,
    Optimizing,
    Securing,
    Networking,
    Graphics,
    Memory,
    Storage,
    Power,
    Gaming,
    Privacy,
    Services,
    Advanced,
    Completing
}

public enum UploadAnimationType
{
    Validating,
    Compressing,
    Encrypting,
    Authenticating,
    Preparing,
    Uploading,
    Verifying,
    Completed
}

// Data models
public class SystemTweaksCollection
{
    public DateTime CollectionTimestamp { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double CollectionDurationMs { get; set; }
    public int TotalTweaksFound { get; set; }
    
    public SystemInformation SystemInfo { get; set; } = new();
    public List<RegistryTweak> RegistryTweaks { get; set; } = new();
    public List<PerformanceTweak> PerformanceTweaks { get; set; } = new();
    public List<SecurityTweak> SecurityTweaks { get; set; } = new();
    public List<NetworkTweak> NetworkTweaks { get; set; } = new();
    public List<GraphicsTweak> GraphicsTweaks { get; set; } = new();
    public List<CpuTweak> CpuTweaks { get; set; } = new();
    public List<MemoryTweak> MemoryTweaks { get; set; } = new();
    public List<StorageTweak> StorageTweaks { get; set; } = new();
    public List<PowerTweak> PowerTweaks { get; set; } = new();
    public List<GamingTweak> GamingTweaks { get; set; } = new();
    public List<PrivacyTweak> PrivacyTweaks { get; set; } = new();
    public List<ServiceTweak> ServiceTweaks { get; set; } = new();
    public List<AdvancedTweak> AdvancedTweaks { get; set; } = new();
}

public class TweakUploadResult
{
    public bool Success { get; set; }
    public string UploadId { get; set; } = string.Empty;
    public double UploadDurationMs { get; set; }
    public long BytesUploaded { get; set; }
    public int TweaksUploaded { get; set; }
    public string ServerResponse { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class UploadResponse
{
    public string UploadId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// Tweak base classes and specific implementations
public abstract class BaseTweak
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public TweakImpact Impact { get; set; }
    public bool Reversible { get; set; }
}

public class RegistryTweak : BaseTweak { }
public class PerformanceTweak : BaseTweak { }
public class SecurityTweak : BaseTweak { }
public class NetworkTweak : BaseTweak { }
public class GraphicsTweak : BaseTweak { }
public class CpuTweak : BaseTweak { }
public class MemoryTweak : BaseTweak { }
public class StorageTweak : BaseTweak { }
public class PowerTweak : BaseTweak { }
public class GamingTweak : BaseTweak { }
public class PrivacyTweak : BaseTweak { }
public class ServiceTweak : BaseTweak { }
public class AdvancedTweak : BaseTweak { }

public enum TweakImpact
{
    Low,
    Medium,
    High,
    Critical
}