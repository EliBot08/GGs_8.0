using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace GGs.Server.Middleware;

public sealed class RequireClientCertForPathsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _cfg;
    private static readonly string[] _protectedPaths = new[] {
        "/api/audit/log",
        "/api/v1/audit/log",
        "/api/ingest/events",
        "/api/v1/ingest/events",
        "/api/devices/enroll",
        "/api/v1/devices/enroll"
    };

    public RequireClientCertForPathsMiddleware(RequestDelegate next, IConfiguration cfg)
    {
        _next = next;
        _cfg = cfg;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only enforce when enabled
        if (!_cfg.GetValue<bool>("Security:ClientCertificate:Enabled"))
        {
            await _next(context);
            return;
        }

        if (HttpMethods.IsPost(context.Request.Method))
        {
            var path = context.Request.Path.ToString();
            if (_protectedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
                var hasTestHeader = !env.IsProduction() && context.Request.Headers.ContainsKey("X-Debug-ClientCert");
                var cert = await context.Connection.GetClientCertificateAsync();
                if (cert == null && !hasTestHeader)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Client certificate required");
                    return;
                }
            }
        }

        await _next(context);
    }
}
