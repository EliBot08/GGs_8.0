using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GGs.E2ETests;

public class AutoUpdateServiceTests
{
    private sealed class MockHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public MockHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsInfo_WhenNewer_NoSignature()
    {
        // Arrange
        var feedUrl = "http://localhost/feed";
        var info = new UpdateInfo
        {
            Version = "65535.0.0.0",
            Channel = "stable",
            Url = "http://localhost/download/ggs-65535.0.0.0.exe",
            Notes = "test",
            Sha256 = null,
            Signature = null
        };
        var json = JsonSerializer.Serialize(info);
        var handler = new MockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var http = new HttpClient(handler);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Update:FeedUrl"] = feedUrl,
            ["Update:Channel"] = "stable"
        }).Build();

        var svc = new AutoUpdateService(cfg, http);

        // Act
        var res = await svc.CheckForUpdatesAsync();

        // Assert
        Assert.NotNull(res);
        Assert.Equal("65535.0.0.0", res!.Version);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_Fails_WhenSignatureInvalid()
    {
        // Arrange: generate a key and set public key in config, but provide a bad signature
        using var rsa = RSA.Create(2048);
        var pub = ExportPublicKeyPem(rsa);
        var feedUrl = "http://localhost/feed";
        var info = new UpdateInfo
        {
            Version = "65535.0.0.0",
            Channel = "stable",
            Url = "http://localhost/download/ggs-65535.0.0.0.exe",
            Notes = "test",
            Sha256 = "abc123",
            Signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("not-a-real-signature"))
        };
        var json = JsonSerializer.Serialize(info);
        var handler = new MockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var http = new HttpClient(handler);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Update:FeedUrl"] = feedUrl,
            ["Update:Channel"] = "stable",
            ["Update:PublicKeyPem"] = pub
        }).Build();

        var svc = new AutoUpdateService(cfg, http);

        // Act
        var res = await svc.CheckForUpdatesAsync();

        // Assert: invalid signature -> returns null
        Assert.Null(res);
    }

    [Fact]
    public async Task DownloadAndInstallAsync_ReturnsFalse_OnHashMismatch()
    {
        // Arrange: serve a small payload but expect a different hash
        var payload = Encoding.UTF8.GetBytes("hello world");
        var handler = new MockHandler(req =>
        {
            if (req.Method == HttpMethod.Get)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload)
                };
                resp.Content.Headers.ContentLength = payload.Length;
                return resp;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var http = new HttpClient(handler);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Update:FeedUrl"] = "http://localhost/feed",
            ["Update:Channel"] = "stable"
        }).Build();
        var svc = new AutoUpdateService(cfg, http);
        var info = new UpdateInfo
        {
            Version = "65535.0.0.0",
            Channel = "stable",
            Url = "http://localhost/download/ggs-65535.0.0.0.exe",
            Sha256 = "deadbeef" // wrong
        };

        // Act
        var (ok, message) = await svc.DownloadAndInstallAsync(info, progress: null, ct: default, launchInstaller: false);

        // Assert
        Assert.False(ok);
        Assert.Contains("hash", message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExportPublicKeyPem(RSA rsa)
    {
        var pub = rsa.ExportSubjectPublicKeyInfo();
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PUBLIC KEY-----");
        sb.AppendLine(Convert.ToBase64String(pub, Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END PUBLIC KEY-----");
        return sb.ToString();
    }
}

