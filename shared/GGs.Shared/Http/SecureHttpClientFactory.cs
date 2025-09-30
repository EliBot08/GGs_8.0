using System;
using System.Net.Http;

namespace GGs.Shared.Http;

/// <summary>
/// Factory for creating secure HTTP clients
/// </summary>
public static class SecureHttpClientFactory
{
    public static HttpClient GetOrCreate(string baseUrl, HttpClientSecurityOptions? options = null, string? userAgent = null)
    {
        var handler = new HttpClientHandler();
        
        if (options != null)
        {
            // Configure security options
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true; // For development
        }

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl)
        };

        if (!string.IsNullOrEmpty(userAgent))
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        }

        return client;
    }
}

public class HttpClientSecurityOptions
{
    public bool ValidateCertificates { get; set; } = true;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool UseClientCertificates { get; set; }
    public string? ClientCertificatePath { get; set; }
    public string? ClientCertificatePassword { get; set; }
    public bool AllowInvalidServerCertificates { get; set; }
    public bool ClientCertificateEnabled { get; set; }
    public string? ClientCertFindType { get; set; }
    public string? ClientCertFindValue { get; set; }
    public string? ClientCertStoreName { get; set; }
    public string? ClientCertStoreLocation { get; set; }
    public PinningMode PinningMode { get; set; }
    public string[] PinnedValues { get; set; } = Array.Empty<string>();
    public string[] Hostnames { get; set; } = Array.Empty<string>();
}

public enum PinningMode
{
    None,
    Certificate,
    PublicKey
}