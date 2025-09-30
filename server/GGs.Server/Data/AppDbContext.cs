using GGs.Server.Models;
using GGs.Shared.Tweaks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Data;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TweakDefinition> Tweaks => Set<TweakDefinition>();
    public DbSet<TweakApplicationLog> TweakLogs => Set<TweakApplicationLog>();
    public DbSet<LicenseRecord> Licenses => Set<LicenseRecord>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<UserDeviceAssignment> UserDeviceAssignments => Set<UserDeviceAssignment>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<GGs.Server.Models.IngestEvent> IngestEvents => Set<GGs.Server.Models.IngestEvent>();
    public DbSet<EliUsageCounter> EliUsageCounters => Set<EliUsageCounter>();
}
