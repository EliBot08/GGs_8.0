using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapControllers();
app.MapGet("/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/ready", () => Results.Ok(new { status = "ready" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

namespace GGs.Server { public partial class Program { } }
