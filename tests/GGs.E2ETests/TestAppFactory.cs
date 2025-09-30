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

namespace GGs.E2ETests;

public sealed class TestAppFactory : WebApplicationFactory<GGs.Server.Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ggs_e2e_{Guid.NewGuid():N}.db");

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
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Try to apply migrations first
            try { db.Database.Migrate(); } catch { }

            // Fallback: ensure tables exist for tests that assume them
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
                );");
                db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_DeviceRegistrations_DeviceId ON DeviceRegistrations(DeviceId);");
                db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_DeviceRegistrations_IsActive ON DeviceRegistrations(IsActive);");
                db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_DeviceRegistrations_LastSeenUtc ON DeviceRegistrations(LastSeenUtc);");

                // Ensure MetadataJson exists on AspNetUsers (for environments that used EnsureCreated previously)
                try { db.Database.ExecuteSqlRaw("ALTER TABLE AspNetUsers ADD COLUMN MetadataJson TEXT"); } catch { }

                // Ensure license lifecycle columns
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN AssignedDevicesJson TEXT NOT NULL DEFAULT '[]'"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN DeveloperMode INTEGER NOT NULL DEFAULT 0"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN MaxDevices INTEGER NOT NULL DEFAULT 1"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN Notes TEXT NULL"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN Status TEXT NOT NULL DEFAULT 'Active'"); } catch { }
                try { db.Database.ExecuteSqlRaw("ALTER TABLE Licenses ADD COLUMN UsageCount INTEGER NOT NULL DEFAULT 0"); } catch { }
            }
            catch { }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }
}

