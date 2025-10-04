using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GGs.Shared.Privacy;

/// <summary>
/// Privacy tier classification for telemetry and system data.
/// Implements Prompt 5: Privacy tiering with signal classification, redaction, and hashing.
/// </summary>
public enum PrivacyTier
{
    /// <summary>
    /// Public data - No privacy concerns (e.g., OS version, processor count)
    /// </summary>
    Public = 0,
    
    /// <summary>
    /// Internal data - Low privacy risk (e.g., performance metrics, health scores)
    /// </summary>
    Internal = 1,
    
    /// <summary>
    /// Confidential data - Medium privacy risk (e.g., machine name, IP addresses)
    /// Requires hashing or redaction in logs
    /// </summary>
    Confidential = 2,
    
    /// <summary>
    /// Restricted data - High privacy risk (e.g., user names, file paths, registry values)
    /// Must be hashed or fully redacted
    /// </summary>
    Restricted = 3,
    
    /// <summary>
    /// Secret data - Critical privacy risk (e.g., credentials, tokens, keys)
    /// Must never be logged or transmitted
    /// </summary>
    Secret = 4
}

/// <summary>
/// Privacy configuration flags to control data collection scope.
/// </summary>
public sealed class PrivacyConfiguration
{
    /// <summary>
    /// Enable collection of machine names (hashed by default)
    /// </summary>
    public bool CollectMachineNames { get; set; } = true;
    
    /// <summary>
    /// Enable collection of user names (hashed by default)
    /// </summary>
    public bool CollectUserNames { get; set; } = false;
    
    /// <summary>
    /// Enable collection of IP addresses (hashed by default)
    /// </summary>
    public bool CollectIpAddresses { get; set; } = true;
    
    /// <summary>
    /// Enable collection of file paths (redacted by default)
    /// </summary>
    public bool CollectFilePaths { get; set; } = false;
    
    /// <summary>
    /// Enable collection of registry values (redacted by default)
    /// </summary>
    public bool CollectRegistryValues { get; set; } = false;
    
    /// <summary>
    /// Enable collection of process names
    /// </summary>
    public bool CollectProcessNames { get; set; } = true;
    
    /// <summary>
    /// Enable collection of network adapter details
    /// </summary>
    public bool CollectNetworkDetails { get; set; } = true;
    
    /// <summary>
    /// Enable detailed health telemetry
    /// </summary>
    public bool EnableDetailedHealthTelemetry { get; set; } = false;
    
    /// <summary>
    /// Hash sensitive fields instead of redacting them (allows correlation while preserving privacy)
    /// </summary>
    public bool HashSensitiveFields { get; set; } = true;
    
    /// <summary>
    /// Load configuration from environment variables or config file
    /// </summary>
    public static PrivacyConfiguration LoadFromEnvironment()
    {
        return new PrivacyConfiguration
        {
            CollectMachineNames = GetBoolEnv("GGS_PRIVACY_COLLECT_MACHINE_NAMES", true),
            CollectUserNames = GetBoolEnv("GGS_PRIVACY_COLLECT_USER_NAMES", false),
            CollectIpAddresses = GetBoolEnv("GGS_PRIVACY_COLLECT_IP_ADDRESSES", true),
            CollectFilePaths = GetBoolEnv("GGS_PRIVACY_COLLECT_FILE_PATHS", false),
            CollectRegistryValues = GetBoolEnv("GGS_PRIVACY_COLLECT_REGISTRY_VALUES", false),
            CollectProcessNames = GetBoolEnv("GGS_PRIVACY_COLLECT_PROCESS_NAMES", true),
            CollectNetworkDetails = GetBoolEnv("GGS_PRIVACY_COLLECT_NETWORK_DETAILS", true),
            EnableDetailedHealthTelemetry = GetBoolEnv("GGS_PRIVACY_DETAILED_HEALTH", false),
            HashSensitiveFields = GetBoolEnv("GGS_PRIVACY_HASH_SENSITIVE", true)
        };
    }
    
    private static bool GetBoolEnv(string key, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }
}

/// <summary>
/// Privacy-aware data sanitizer with classification, redaction, and hashing.
/// </summary>
public static class PrivacySanitizer
{
    private static readonly PrivacyConfiguration _config = PrivacyConfiguration.LoadFromEnvironment();
    
    /// <summary>
    /// Sanitize a value based on its privacy tier and configuration.
    /// </summary>
    public static string Sanitize(string? value, PrivacyTier tier)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        
        return tier switch
        {
            PrivacyTier.Public => value,
            PrivacyTier.Internal => value,
            PrivacyTier.Confidential => _config.HashSensitiveFields ? Hash(value) : Redact(value),
            PrivacyTier.Restricted => _config.HashSensitiveFields ? Hash(value) : "[REDACTED]",
            PrivacyTier.Secret => "[SECRET]",
            _ => "[UNKNOWN]"
        };
    }
    
    /// <summary>
    /// Sanitize machine name based on configuration.
    /// </summary>
    public static string SanitizeMachineName(string? machineName)
    {
        if (!_config.CollectMachineNames) return "[REDACTED]";
        return Sanitize(machineName, PrivacyTier.Confidential);
    }
    
    /// <summary>
    /// Sanitize user name based on configuration.
    /// </summary>
    public static string SanitizeUserName(string? userName)
    {
        if (!_config.CollectUserNames) return "[REDACTED]";
        return Sanitize(userName, PrivacyTier.Restricted);
    }
    
    /// <summary>
    /// Sanitize IP address based on configuration.
    /// </summary>
    public static string SanitizeIpAddress(string? ipAddress)
    {
        if (!_config.CollectIpAddresses) return "[REDACTED]";
        return Sanitize(ipAddress, PrivacyTier.Confidential);
    }
    
    /// <summary>
    /// Sanitize file path based on configuration.
    /// </summary>
    public static string SanitizeFilePath(string? filePath)
    {
        if (!_config.CollectFilePaths) return "[REDACTED]";
        if (string.IsNullOrWhiteSpace(filePath)) return string.Empty;
        
        // Redact user-specific parts of paths
        var sanitized = filePath
            .Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "[USER_PROFILE]")
            .Replace(Environment.UserName, "[USERNAME]");
        
        return _config.HashSensitiveFields ? Hash(sanitized) : sanitized;
    }
    
    /// <summary>
    /// Sanitize registry value based on configuration.
    /// </summary>
    public static string SanitizeRegistryValue(string? registryValue)
    {
        if (!_config.CollectRegistryValues) return "[REDACTED]";
        return Sanitize(registryValue, PrivacyTier.Restricted);
    }
    
    /// <summary>
    /// Sanitize process name based on configuration.
    /// </summary>
    public static string SanitizeProcessName(string? processName)
    {
        if (!_config.CollectProcessNames) return "[REDACTED]";
        return Sanitize(processName, PrivacyTier.Internal);
    }
    
    /// <summary>
    /// Hash a value using SHA256 for privacy-preserving correlation.
    /// </summary>
    public static string Hash(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash)[..16]; // First 16 chars for brevity
    }
    
    /// <summary>
    /// Redact a value by showing only first and last characters.
    /// </summary>
    public static string Redact(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (value.Length <= 4) return "****";
        return $"{value[0]}***{value[^1]}";
    }
    
    /// <summary>
    /// Sanitize an object by applying privacy rules to all string properties.
    /// </summary>
    public static T? SanitizeObject<T>(T? obj, Dictionary<string, PrivacyTier> propertyTiers) where T : class
    {
        if (obj == null) return null;
        
        var type = typeof(T);
        foreach (var (propertyName, tier) in propertyTiers)
        {
            var property = type.GetProperty(propertyName);
            if (property == null || property.PropertyType != typeof(string)) continue;
            
            var value = property.GetValue(obj) as string;
            if (value != null)
            {
                property.SetValue(obj, Sanitize(value, tier));
            }
        }
        
        return obj;
    }
    
    /// <summary>
    /// Get current privacy configuration.
    /// </summary>
    public static PrivacyConfiguration GetConfiguration() => _config;
}

/// <summary>
/// Privacy-aware telemetry data model.
/// </summary>
public sealed class PrivacyAwareTelemetry
{
    public string DeviceId { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Public tier - always collected
    public string AgentVersion { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    
    // Confidential tier - hashed or redacted
    public string MachineNameHash { get; set; } = string.Empty;
    public string IpAddressHash { get; set; } = string.Empty;
    
    // Internal tier - collected if enabled
    public Dictionary<string, object> PerformanceMetrics { get; set; } = new();
    public Dictionary<string, object> HealthMetrics { get; set; } = new();
    
    // Privacy metadata
    public PrivacyConfiguration PrivacySettings { get; set; } = new();
    public Dictionary<string, PrivacyTier> FieldClassifications { get; set; } = new();
}

