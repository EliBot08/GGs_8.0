using System.Security.Claims;

namespace GGs.Server.Middleware;

public sealed class TenantMiddleware
{
    private readonly RequestDelegate _next;
    public TenantMiddleware(RequestDelegate next) { _next = next; }
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                      ?? context.User?.FindFirst("tenant_id")?.Value
                      ?? "default";
        context.Items["TenantId"] = tenantId;
        await _next(context);
    }
}


