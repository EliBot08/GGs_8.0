using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GGs.Server.Controllers;

[ApiController]
[Route("api/docs")] // lightweight deployment docs endpoint
[AllowAnonymous]
public sealed class DocsController : ControllerBase
{
    [HttpGet("deployment")]
    public IActionResult Deployment()
    {
        var md = @"# GGs Deployment Guide

## Prerequisites
- .NET 8 runtime
- SQLite (bundled) or switch to SQL Server
- Configure environment variables:
  - Auth:JwtKey or Auth:SigningKeys[0]:key, Auth:SigningKeys[0]:kid
  - License:PublicKeyPem (required in production)
  - Otel:ServerEnabled=true and Otel:OtlpEndpoint
  - Azure:Monitor:ConnectionString (optional)
  - Security:ClientCertificate:Enabled=true for mTLS enrollments

## Running
- dotnet run -c Release in server/GGs.Server
- Browse /ready and /swagger

## Desktop
- Build GGs.Desktop; set GGS_SERVER_URL env var
";
        return Content(md, "text/markdown");
    }
}


