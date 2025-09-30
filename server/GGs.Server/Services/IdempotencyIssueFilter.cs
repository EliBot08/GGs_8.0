using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GGs.Server.Data;
using GGs.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GGs.Server.Services;

public sealed class IdempotencyIssueFilter : IAsyncActionFilter
{
    private readonly AppDbContext _db;
    private readonly ILogger<IdempotencyIssueFilter> _logger;
    public IdempotencyIssueFilter(AppDbContext db, ILogger<IdempotencyIssueFilter> logger) { _db = db; _logger = logger; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method)) { await next(); return; }
if (!(context.HttpContext.Request.Path.Value!.Contains("/api/licenses/issue", StringComparison.OrdinalIgnoreCase) 
    || context.HttpContext.Request.Path.Value!.Contains("/api/v1/licenses/issue", StringComparison.OrdinalIgnoreCase))) { await next(); return; }

        if (!context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var keyVals) || string.IsNullOrWhiteSpace(keyVals))
        {
            await next();
            return;
        }
        var key = keyVals.ToString();

        // Compute request hash from action argument (LicenseIssueRequest)
        string reqHash = "";
        try
        {
            var bodyArg = context.ActionArguments.Values.FirstOrDefault();
            var json = JsonSerializer.Serialize(bodyArg);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
            reqHash = Convert.ToHexString(hash);
        }
        catch { }

        var existing = _db.IdempotencyRecords.FirstOrDefault(r => r.Key == key);
        if (existing != null)
        {
            if (!string.Equals(existing.RequestHash, reqHash, StringComparison.Ordinal))
            {
                context.Result = new ConflictObjectResult(new { error = "Idempotency key already used with different request." });
                return;
            }
            context.Result = new ContentResult { Content = existing.ResponseJson, StatusCode = existing.StatusCode, ContentType = "application/json" };
            return;
        }

        // Execute action
        var executed = await next();
        try
        {
            if (executed.Result is ObjectResult ok)
            {
                var json = JsonSerializer.Serialize(ok.Value);
                var rec = new IdempotencyRecord
                {
                    Key = key,
                    RequestHash = reqHash,
                    ResponseJson = json,
                    StatusCode = ok.StatusCode ?? 200,
                    CreatedUtc = DateTime.UtcNow
                };
                _db.IdempotencyRecords.Add(rec);
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist idempotency record");
        }
    }
}

