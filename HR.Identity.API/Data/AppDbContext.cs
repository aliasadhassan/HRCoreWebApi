using HR.Identity.API.Models;
using HR.Identity.API.Models.Common;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Helpers;
using HR.Shared.Library.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Data;

/// <param name="http">Audit ke liye (kisne, kis request se). Design-time / tests mein null — tab audit nahi likha jata.</param>
public class AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? http = null) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Ek request (scope) ke saare audit rows isi id ke saath — Activity mein ek line.</summary>
    private readonly Guid _correlationId = Guid.NewGuid();

    /// <summary>Audit mein ye cheezen: company, settings, license, users, SSO links. Roles ke join rows neeche alag.</summary>
    private static readonly HashSet<Type> Audited =
        [typeof(Tenant), typeof(TenantSettings), typeof(TenantSubscription), typeof(User), typeof(UserExternalLogin), typeof(Role)];

    /// <summary>Har login pe badalne wale fields — shor, audit nahi (sign-in history Login activity mein hai).</summary>
    private static readonly HashSet<string> Noisy =
        ["LastLoginAt", "AccessFailedCount", "LockoutEnd", "SecurityStamp", "NormalizedEmail", "NormalizedName", "LastUsedAt"];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity ka apna schema (employee/payroll ki tarah), public mein nahi
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        // har DateTime column = timestamptz(3)
        c.Properties<DateTime>().HavePrecision(3);
        c.Properties<DateTime?>().HavePrecision(3);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var audit = await CaptureAuditAsync(ct);
        foreach (var e in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (e.State)
            {
                case EntityState.Added:
                    e.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    e.Entity.UpdatedAt = now;
                    break;
                case EntityState.Deleted:          // hard delete -> soft delete
                    e.State = EntityState.Modified;
                    e.Entity.IsDeleted = true;
                    e.Entity.UpdatedAt = now;
                    break;
            }
        }
        if (audit is { Count: > 0 } && http?.HttpContext?.User is { } principal && principal.GetTenantId() is { } tenantId)
        {
            var userId = principal.GetUserId();
            var who = principal.FindFirst(JwtTokenHelper.DisplayNameClaim)?.Value ?? principal.Identity?.Name;
            AuditLogs.AddRange(audit.Select(c => AuditLog.From(tenantId, c, now, userId, who, Operation(), _correlationId)));
        }
        return await base.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sirf logged-in admin ke kaam (token mein tenant ho). Login / reset / invite accept jaise anonymous kaam yahan nahi —
    /// woh Login activity mein hain.
    /// </summary>
    private async Task<List<AuditCapture>?> CaptureAuditAsync(CancellationToken ct)
    {
        if (http?.HttpContext?.User?.GetTenantId() is null)
            return null;

        var result = AuditTrail.Capture(ChangeTracker, Audited.Contains, skipFields: (_, field) => Noisy.Contains(field));

        // UserRole: user ko role mila / hata — EntityId = user, field "Role"
        foreach (var e in ChangeTracker.Entries<UserRole>().Where(e => e.State is EntityState.Added or EntityState.Deleted).ToList())
        {
            var user = await FindAsync(Users, e.Entity.UserId, ct);
            var role = await FindAsync(Roles, e.Entity.RoleId, ct);
            var added = e.State == EntityState.Added;
            result.Add(new AuditCapture(nameof(UserRole), e.Entity.UserId, added ? AuditAction.Created : AuditAction.Deleted,
                user?.DisplayName, user?.EmployeeId,
                [new AuditFieldChange("Role", added ? null : role?.Name, added ? role?.Name : null)]));
        }

        // RolePermission: ek role ki saari di / li gayi permissions ek hi row mein
        var grants = ChangeTracker.Entries<RolePermission>()
            .Where(e => e.State is EntityState.Added or EntityState.Deleted).ToList();
        foreach (var byRole in grants.GroupBy(e => e.Entity.RoleId))
        {
            var role = await FindAsync(Roles, byRole.Key, ct);
            var ids = byRole.Select(e => e.Entity.PermissionId).Distinct().ToList();
            var codes = await Permissions.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
            // Hata kar wapis di gayi permission = koi badlaav nahi
            var changes = byRole
                .GroupBy(e => e.Entity.PermissionId)
                .Where(g => g.Select(e => e.State).Distinct().Count() == 1)
                .Select(g => (Code: codes.GetValueOrDefault(g.Key) ?? g.Key.ToString(), Added: g.First().State == EntityState.Added))
                .OrderBy(x => x.Code)
                .Select(x => new AuditFieldChange("Permission", x.Added ? null : x.Code, x.Added ? x.Code : null))
                .ToList();
            if (changes.Count > 0)
                result.Add(new AuditCapture(nameof(RolePermission), byRole.Key, AuditAction.Updated, role?.Name, null, changes));
        }
        return result;
    }

    /// <summary>Pehle tracker (abhi bana user / role), phir database.</summary>
    private static async Task<T?> FindAsync<T>(DbSet<T> set, Guid id, CancellationToken ct) where T : AuditableEntity
        => set.Local.FirstOrDefault(x => x.Id == id) ?? await set.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    private string? Operation()
    {
        var ctx = http?.HttpContext;
        if (ctx is null)
            return null;
        var route = (ctx.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? ctx.Request.Path.Value;
        return $"{ctx.Request.Method} {route}";
    }
}
