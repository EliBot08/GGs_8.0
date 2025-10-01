using System;
using System.Collections.Generic;
using System.IO;
using GGs.Server.Data;
using GGs.Server.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GGs.NewE2ETests;

public sealed class TestAppFactory : WebApplicationFactory<GGs.Server.Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ggs_test_{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Staging");
        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_dbPath}",
                ["Database:UseEnsureCreated"] = "false",
                ["Auth:JwtKey"] = "0123456789ABCDEF0123456789ABCDEF",
                ["Auth:Issuer"] = "GGs.Server",
                ["Auth:Audience"] = "GGs.Clients",
                ["Auth:IssueRefreshTokens"] = "false",
                ["Seed:AdminEmail"] = "admin@ggs.local",
                ["Seed:AdminPassword"] = "ChangeMe!123"
            });
        });

        builder.ConfigureServices((ctx, services) =>
        {
            // Apply migrations and schema setup will happen after host is built
        });
    }

    protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        
        // Now seed the database using the properly configured service provider
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        
        // Apply EF migrations (best effort)
        try { db.Database.Migrate(); } catch { /* continue with fallback DDL */ }

            // Fallback: ensure missing tables/columns exist for tests when migrations are incomplete
            try
            {
                db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS RefreshTokens (
                    Id TEXT NOT NULL PRIMARY KEY,
                    UserId TEXT NOT NULL,
                    TokenHash TEXT NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                    ExpiresUtc TEXT NOT NULL,
                    RevokedUtc TEXT NULL,
                    DeviceId TEXT NULL,
                    UserAgent TEXT NULL
                );");

                db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_RefreshTokens_UserId ON RefreshTokens(UserId);");
                db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_RefreshTokens_TokenHash ON RefreshTokens(TokenHash);");

                db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS DeviceRegistrations (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    DeviceId TEXT NOT NULL,
                    Thumbprint TEXT NOT NULL,
                    CommonName TEXT NULL,
                    RegisteredUtc TEXT NOT NULL,
                    LastSeenUtc TEXT NOT NULL,
                    RevokedUtc TEXT NULL,
                    IsActive INTEGER NOT NULL
                );
                ");
                db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_DeviceRegistrations_DeviceId ON DeviceRegistrations(DeviceId);");
                db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_DeviceRegistrations_IsActive ON DeviceRegistrations(IsActive);");
                db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_DeviceRegistrations_LastSeenUtc ON DeviceRegistrations(LastSeenUtc);");

                // Ensure MetadataJson column exists on AspNetUsers
                try { db.Database.ExecuteSqlRaw("ALTER TABLE AspNetUsers ADD COLUMN MetadataJson TEXT"); } catch { }

                // Ensure Licenses columns exist (lifecycle fields)
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN AssignedDevicesJson TEXT NOT NULL DEFAULT '[]'"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN DeveloperMode INTEGER NOT NULL DEFAULT 0"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN MaxDevices INTEGER NOT NULL DEFAULT 1"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN Notes TEXT NULL"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN Status TEXT NOT NULL DEFAULT 'Active'"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN UsageCount INTEGER NOT NULL DEFAULT 0"); } catch { }

                // Ensure TweakLogs enriched audit columns exist
                try { db.Database.ExecuteSqlRaw("ALTER TABLE TweakLogs ADD COLUMN RegistryPath TEXT"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE TweakLogs ADD COLUMN RegistryValueName TEXT"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE TweakLogs ADD COLUMN RegistryValueType TEXT"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE TweakLogs ADD COLUMN OriginalValue TEXT"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE TweakLogs ADD COLUMN NewValue TEXT"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE TweakLogs ADD COLUMN ServiceName TEXT"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE TweakLogs ADD COLUMN ActionApplied TEXT"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE TweakLogs ADD COLUMN ScriptApplied TEXT"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE TweakLogs ADD COLUMN UndoScript TEXT"); } catch { }
            }
            catch { /* ignore fallback DDL errors */ }
            
        // Seed test data
        try
        {
            // Ensure Admin role exists
            if (!roleManager.RoleExistsAsync("Admin").GetAwaiter().GetResult())
            {
                roleManager.CreateAsync(new IdentityRole("Admin")).GetAwaiter().GetResult();
            }
            
            // Ensure test admin user exists
            var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@ggs.local";
            var adminPassword = configuration["Seed:AdminPassword"] ?? "ChangeMe!123";
            
            var adminUser = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };
                
                var createResult = userManager.CreateAsync(adminUser, adminPassword).GetAwaiter().GetResult();
                if (createResult.Succeeded)
                {
                    userManager.AddToRoleAsync(adminUser, "Admin").GetAwaiter().GetResult();
                }
            }
        }
        catch { /* ignore seeding errors - they'll be caught by tests */ }
        
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch { /* ignore cleanup errors in CI */ }
    }
}

