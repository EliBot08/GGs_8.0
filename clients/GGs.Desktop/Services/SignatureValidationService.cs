using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using GGs.Desktop.ViewModels.Admin; // For CloudProfile

namespace GGs.Desktop.Services
{
    /// <summary>
    /// Handles digital signature verification and trust settings for cloud profiles.
    /// Minimal implementation to satisfy current usage; can be expanded to perform real PKI/metadata checks.
    /// </summary>
    public sealed class SignatureValidationService
    {
        private readonly IConfiguration _cfg;
        private readonly string _trustedPublishersPath;

        public SignatureValidationService()
        {
            _cfg = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var baseDirOverride = Environment.GetEnvironmentVariable("GGS_DATA_DIR");
            var baseDir = !string.IsNullOrWhiteSpace(baseDirOverride)
                ? baseDirOverride!
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(baseDir, "GGs", "security");
            Directory.CreateDirectory(dir);
            _trustedPublishersPath = Path.Combine(dir, "trusted_publishers.json");
        }

        public TrustSettings GetTrustSettings()
        {
            // Default to requiring signature validation unless explicitly disabled
            var require = true;
            if (bool.TryParse(_cfg["Security:Trust:RequireSignatureValidation"], out var cfgRequire))
                require = cfgRequire;
            return new TrustSettings { RequireSignatureValidation = require };
        }

        /// <summary>
        /// Returns trusted publishers list from a local file if present; otherwise returns an empty list.
        /// File format (JSON): [{ "name": "Org", "fingerprints": ["abc", "def"] }]
        /// </summary>
        public async Task<IReadOnlyList<TrustedPublisher>> GetTrustedPublishersAsync()
        {
            try
            {
                if (File.Exists(_trustedPublishersPath))
                {
                    await using var fs = File.OpenRead(_trustedPublishersPath);
                    var list = await JsonSerializer.DeserializeAsync<List<TrustedPublisher>>(fs, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return (IReadOnlyList<TrustedPublisher>)(list ?? new List<TrustedPublisher>());
                }
            }
            catch { /* ignore and return empty */ }
            return Array.Empty<TrustedPublisher>();
        }

        /// <summary>
        /// Naive signature verification: considers a profile verified if the publisher or fingerprint matches a trusted entry.
        /// This can be extended to pull publisher certificates or verify signed manifests.
        /// </summary>
        public async Task<bool> VerifyProfileSignatureAsync(CloudProfile profile)
        {
            if (profile == null) return false;
            try
            {
                var trusted = await GetTrustedPublishersAsync();
                var publisherTrusted = trusted.Any(tp => string.Equals(tp.Name, profile.PublisherName, StringComparison.OrdinalIgnoreCase));
                var fingerprintTrusted = !string.IsNullOrWhiteSpace(profile.SignatureFingerprint) &&
                                          trusted.Any(tp => tp.Fingerprints.Contains(profile.SignatureFingerprint, StringComparer.OrdinalIgnoreCase));
                return publisherTrusted || fingerprintTrusted;
            }
            catch
            {
                return false;
            }
        }
    }

    public sealed class TrustSettings
    {
        public bool RequireSignatureValidation { get; set; } = true;
    }

    public sealed class TrustedPublisher
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Fingerprints { get; set; } = new();
    }
}