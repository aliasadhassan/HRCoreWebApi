using HR.Identity.API.Models;
using HR.Identity.API.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        // har DateTime column = datetime2(3)
        c.Properties<DateTime>().HavePrecision(3);
        c.Properties<DateTime?>().HavePrecision(3);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
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
        return base.SaveChangesAsync(ct);
    }
}
