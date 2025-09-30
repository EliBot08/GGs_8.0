using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using Xunit;

namespace GGs.E2ETests;

public class AuthServiceTests
{
    private class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handle;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) => _handle = handle;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handle(request));
    }

    [Fact]
    public async Task Login_And_EnsureAccessToken_ShouldPersistAndReturnToken()
    {
        var temp = Path.Combine(Path.GetTempPath(), "ggs_auth_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_DATA_DIR", temp);
        try
        {
            var handler = new StubHandler(req =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("/api/auth/login"))
                {
                    var json = JsonSerializer.Serialize(new { accessToken = "ATOKEN", expiresIn = 3600, refreshToken = "RTOKEN", refreshExpiresIn = 86400, roles = new[] { "User" } });
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://stub/") };
            var auth = new AuthService(http);
            var ok = await auth.LoginAsync("user", "pass");
            Assert.True(ok.ok);
            var (tokOk, token) = await auth.EnsureAccessTokenAsync();
            Assert.True(tokOk);
            Assert.Equal("ATOKEN", token);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_DATA_DIR", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    [Fact]
    public async Task EnsureAccessToken_ShouldRefreshWhenExpired()
    {
        var temp = Path.Combine(Path.GetTempPath(), "ggs_auth_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_DATA_DIR", temp);
        try
        {
            var loginReturned = false;
            var handler = new StubHandler(req =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("/api/auth/login"))
                {
                    loginReturned = true;
                    var json = JsonSerializer.Serialize(new { accessToken = "AT1", expiresIn = 1, refreshToken = "RT1", refreshExpiresIn = 86400, roles = new[] { "User" } });
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                }
                if (req.RequestUri!.AbsolutePath.EndsWith("/api/auth/refresh"))
                {
                    var json = JsonSerializer.Serialize(new { accessToken = "AT2", expiresIn = 3600, refreshToken = "RT2", refreshExpiresIn = 86400, roles = new[] { "User" } });
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://stub/") };
            var auth = new AuthService(http);
            var ok = await auth.LoginAsync("user", "pass");
            Assert.True(ok.ok);
            await Task.Delay(1200); // let token expire
            var (tokOk, token) = await auth.EnsureAccessTokenAsync();
            Assert.True(tokOk);
            Assert.Equal("AT2", token);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_DATA_DIR", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}

