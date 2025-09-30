using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;
using GGs.Server.Services;

namespace GGs.Server.Middleware;

public class CertificateAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CertificateAuthenticationMiddleware> _logger;

    public CertificateAuthenticationMiddleware(RequestDelegate next, ILogger<CertificateAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IDeviceIdentityService deviceService)
    {
        // Skip for health/public endpoints
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/api/auth/login") ||
            context.Request.Path.StartsWithSegments("/.well-known"))
        {
            await _next(context);
            return;
        }

        // Check for client certificate
        var clientCert = await context.Connection.GetClientCertificateAsync();
        if (clientCert == null)
        {
            // Allow JWT-only auth for backwards compatibility during transition
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                await _next(context);
                return;
            }

            _logger.LogWarning("No client certificate provided from {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Client certificate required");
            return;
        }

        // Validate certificate
        if (!ValidateCertificate(clientCert))
        {
            _logger.LogWarning("Invalid certificate from {IP}: {Subject}", 
                context.Connection.RemoteIpAddress, clientCert.Subject);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid client certificate");
            return;
        }

        // Validate device registration
        var device = await deviceService.ValidateDeviceCertificateAsync(clientCert);
        if (device == null)
        {
            _logger.LogWarning("Unregistered device certificate: {Thumbprint}", clientCert.GetCertHashString());
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Device not registered");
            return;
        }

        // Add device claims
        var claims = new List<Claim>
        {
            new Claim("device_id", device.DeviceId),
            new Claim("device_thumbprint", device.Thumbprint)
        };

        var identity = new ClaimsIdentity(claims, "Certificate");
        context.User = new ClaimsPrincipal(identity);

        await _next(context);
    }

    private bool ValidateCertificate(X509Certificate2 certificate)
    {
        // Basic validation
        if (certificate == null) return false;
        if (DateTime.UtcNow < certificate.NotBefore || DateTime.UtcNow > certificate.NotAfter)
        {
            _logger.LogWarning("Certificate expired or not yet valid: {Subject}", certificate.Subject);
            return false;
        }

        // Chain validation
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

        var isValid = chain.Build(certificate);
        if (!isValid)
        {
            foreach (var status in chain.ChainStatus)
            {
                _logger.LogWarning("Certificate chain error: {Status} - {Info}", 
                    status.Status, status.StatusInformation);
            }
        }

        return isValid;
    }
}
