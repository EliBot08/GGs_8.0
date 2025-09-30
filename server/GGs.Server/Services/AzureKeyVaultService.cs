using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace GGs.Server.Services;

public interface ISecretManager
{
    Task<string> GetSecretAsync(string secretName);
    Task SetSecretAsync(string secretName, string secretValue);
    Task<bool> SecretExistsAsync(string secretName);
}

public sealed class AzureKeyVaultSecretManager : ISecretManager
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<AzureKeyVaultSecretManager> _logger;

    public AzureKeyVaultSecretManager(IConfiguration configuration, ILogger<AzureKeyVaultSecretManager> logger)
    {
        _logger = logger;
        var keyVaultUrl = configuration["Azure:KeyVault:VaultUrl"];
        
        if (string.IsNullOrEmpty(keyVaultUrl))
        {
            throw new InvalidOperationException("Azure:KeyVault:VaultUrl configuration is required for production.");
        }

        // Use DefaultAzureCredential for production (supports managed identity, service principal, etc.)
        var credential = new DefaultAzureCredential();
        _secretClient = new SecretClient(new Uri(keyVaultUrl), credential);
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        try
        {
            var response = await _secretClient.GetSecretAsync(secretName);
            return response.Value.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret {SecretName} from Key Vault", secretName);
            throw;
        }
    }

    public async Task SetSecretAsync(string secretName, string secretValue)
    {
        try
        {
            await _secretClient.SetSecretAsync(secretName, secretValue);
            _logger.LogInformation("Successfully set secret {SecretName} in Key Vault", secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret {SecretName} in Key Vault", secretName);
            throw;
        }
    }

    public async Task<bool> SecretExistsAsync(string secretName)
    {
        try
        {
            await _secretClient.GetSecretAsync(secretName);
            return true;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if secret {SecretName} exists", secretName);
            return false;
        }
    }
}

// Development fallback using configuration
public sealed class ConfigurationSecretManager : ISecretManager
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationSecretManager> _logger;

    public ConfigurationSecretManager(IConfiguration configuration, ILogger<ConfigurationSecretManager> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<string> GetSecretAsync(string secretName)
    {
        var value = _configuration[secretName];
        if (string.IsNullOrEmpty(value))
        {
            _logger.LogWarning("Secret {SecretName} not found in configuration", secretName);
            throw new InvalidOperationException($"Secret {secretName} not found in configuration");
        }
        return Task.FromResult(value);
    }

    public Task SetSecretAsync(string secretName, string secretValue)
    {
        _logger.LogWarning("Cannot set secrets in configuration-based secret manager");
        throw new NotSupportedException("Configuration-based secret manager is read-only");
    }

    public Task<bool> SecretExistsAsync(string secretName)
    {
        var value = _configuration[secretName];
        return Task.FromResult(!string.IsNullOrEmpty(value));
    }
}

// Key rotation service for enhanced security
public interface IKeyRotationService
{
    Task RotateJwtSigningKeyAsync();
    Task RotateLicenseKeysAsync();
    Task<TimeSpan> GetTimeUntilNextRotation();
}

public sealed class AzureKeyRotationService : IKeyRotationService
{
    private readonly ISecretManager _secretManager;
    private readonly ILogger<AzureKeyRotationService> _logger;
    private readonly IConfiguration _configuration;

    public AzureKeyRotationService(
        ISecretManager secretManager, 
        ILogger<AzureKeyRotationService> logger,
        IConfiguration configuration)
    {
        _secretManager = secretManager;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task RotateJwtSigningKeyAsync()
    {
        try
        {
            // Generate new signing key
            var newKey = GenerateSecureKey(256); // 256-bit key
            
            // Store new key with versioning
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            await _secretManager.SetSecretAsync($"auth-jwt-key-{timestamp}", newKey);
            await _secretManager.SetSecretAsync("auth-jwt-key-current", newKey);
            
            _logger.LogInformation("JWT signing key rotated successfully at {Timestamp}", timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate JWT signing key");
            throw;
        }
    }

    public async Task RotateLicenseKeysAsync()
    {
        try
        {
            // Generate new RSA key pair for license signing
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
            var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());

            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            await _secretManager.SetSecretAsync($"license-private-key-{timestamp}", privateKey);
            await _secretManager.SetSecretAsync($"license-public-key-{timestamp}", publicKey);
            await _secretManager.SetSecretAsync("license-private-key-current", privateKey);
            await _secretManager.SetSecretAsync("license-public-key-current", publicKey);

            _logger.LogInformation("License keys rotated successfully at {Timestamp}", timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate license keys");
            throw;
        }
    }

    public Task<TimeSpan> GetTimeUntilNextRotation()
    {
        var rotationIntervalDays = _configuration.GetValue<int>("Security:KeyRotation:IntervalDays", 90);
        var lastRotation = _configuration.GetValue<DateTime>("Security:KeyRotation:LastRotation", DateTime.UtcNow.AddDays(-90));
        var nextRotation = lastRotation.AddDays(rotationIntervalDays);
        
        return Task.FromResult(nextRotation - DateTime.UtcNow);
    }

    private static string GenerateSecureKey(int bitLength)
    {
        var bytes = new byte[bitLength / 8];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
