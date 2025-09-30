using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using Xunit;

namespace GGs.E2ETests;

public class SmokeSecureHttpClientFactoryTests
{
    [Fact]
    public async Task AuthService_Uses_SecureHttpClientFactory_UserAgent()
    {
        // Arrange: pick a free port and start HttpListener
        int port;
        using (var l = new TcpListener(System.Net.IPAddress.Loopback, 0)) { l.Start(); port = ((IPEndPoint)l.LocalEndpoint).Port; }
        var prefix = $"http://localhost:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        string? capturedUA = null;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            capturedUA = ctx.Request.Headers["User-Agent"];
            if (ctx.Request.Url!.AbsolutePath.EndsWith("/api/auth/login"))
            {
                var payload = JsonSerializer.Serialize(new { accessToken = "ATOKEN", expiresIn = 60, refreshToken = "RTOKEN", refreshExpiresIn = 3600, roles = new[] { "Admin" } });
                var bytes = Encoding.UTF8.GetBytes(payload);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            else
            {
                ctx.Response.StatusCode = 404;
            }
            ctx.Response.Close();
        }, cts.Token);

        // Write appsettings.json to current dir so AuthService picks our BaseUrl
        var cwd = Directory.GetCurrentDirectory();
        var appsettingsPath = Path.Combine(cwd, "appsettings.json");
        var originalAppSettingsExists = File.Exists(appsettingsPath);
        string? originalAppSettings = null;
        if (originalAppSettingsExists)
            originalAppSettings = File.ReadAllText(appsettingsPath);
        File.WriteAllText(appsettingsPath, JsonSerializer.Serialize(new { Server = new { BaseUrl = prefix } }));

        try
        {
            // Act: instantiate AuthService without injecting HttpClient so it uses SecureHttpClientFactory
            var auth = new AuthService();
            var (ok, msg) = await auth.LoginAsync("user", "pass");

            // Assert
            Assert.True(ok);
            Assert.NotNull(capturedUA);
            Assert.Contains("GGs.Desktop", capturedUA!);
        }
        finally
        {
            try { listener.Stop(); } catch { }
            // restore appsettings
            if (originalAppSettingsExists && originalAppSettings != null)
                File.WriteAllText(appsettingsPath, originalAppSettings);
            else
                File.Delete(appsettingsPath);
        }
    }
}
