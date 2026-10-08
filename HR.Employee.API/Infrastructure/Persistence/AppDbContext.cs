namespace HR.Employee.API.Infrastructure.Persistence;

using System.Reflection;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Assets;
using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Leaves;
using HR.Employee.API.Domain.Lifecycle;
using HR.Employee.API.Domain.Organization;
using MassTransit;
using HR.Shared.Library.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Repository + UnitOfWork + transaction ki zaroorat nahi — ye sab yahin hai:
///   1. Tenant + soft-delete global query filter (har AuditableEntity pe automatic)
///   2. SaveChanges se PEHLE domain events dispatch (handlers outbox mein message likhte hain → same transaction)
///   3. Audit fields, TenantId, soft delete
/// </summary>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUser currentUser,
    IPublisher publisher) : DbContext(options), IAppDbContext, ITenantSessionContext
{
    private static readonly MethodInfo TenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<JobHistoryEntry> EmployeeJobHistory => Set<JobHistoryEntry>();

    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeavePolicy> LeavePolicies => Set<LeavePolicy>();
    public DbSet<LeaveApprovalSettings> LeaveApprovalSettings => Set<LeaveApprovalSettings>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<RosterEntry> RosterEntries => Set<RosterEntry>();
    public DbSet<AttendancePolicy> AttendancePolicies => Set<AttendancePolicy>();
    public DbSet<AttendanceDevice> AttendanceDevices => Set<AttendanceDevice>();
    public DbSet<AttendanceDay> AttendanceDays => Set<AttendanceDay>();
    public DbSet<AttendanceRequest> AttendanceRequests => Set<AttendanceRequest>();

    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<LifecycleCase> LifecycleCases => Set<LifecycleCase>();

    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();
    public DbSet<AssetEvent> AssetEvents => Set<AssetEvent>();

    private readonly TenantScope _tenant = new(() => currentUser.TenantId);

    /// <summary>Is scope ka tenant (JWT ya consumer message) — RLS interceptor bhi yahi padhta hai.</summary>
    public Guid? SessionTenantId => _tenant.TenantId;

    /// <summary>Query filter har query pe isay parameter ki tarah padhta hai.</summary>
    private Guid CurrentTenantId => _tenant.FilterId;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        // Child rows parent ke saath hide hon — yahi chahiye, warning ka shor band
        => optionsBuilder
            .ConfigureWarnings(w =>
                w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .AddInterceptors(TenantConnectionInterceptor.Instance);   // RLS: har connection pe app.tenant_id

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HavePrecision(3);
        configurationBuilder.Properties<DateTime?>().HavePrecision(3);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("employee");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;
            if (entityType.IsOwned() || !typeof(Entity).IsAssignableFrom(clrType))
                continue;

            modelBuilder.Entity(clrType).Ignore(nameof(Entity.DomainEvents));

            if (typeof(AuditableEntity).IsAssignableFrom(clrType))
                TenantFilterMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
        }

        // MassTransit: InboxState, OutboxMessage, OutboxState
        modelBuilder.AddTransactionalOutboxEntities();
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : AuditableEntity
        => modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId && !e.IsDeleted);

    /// <summary>Background consumer (HTTP user nahi): message ka tenant is scope pe — query filter, audit aur RLS sab.</summary>
    public Task UseTenantAsync(Guid tenantId, CancellationToken ct) => _tenant.UseAsync(Database, tenantId, ct);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(cancellationToken);
        ApplyAuditRules();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (ChangeTracker.Entries<Entity>().Any(e => e.Entity.DomainEvents.Count > 0))
            throw new InvalidOperationException("Use SaveChangesAsync — domain events need async dispatch.");

        ApplyAuditRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private async Task DispatchDomainEventsAsync(CancellationToken ct)
    {
        // Handler naye events raise kar sakta hai — jab tak queue khali na ho
        for (var round = 0; round < 10; round++)
        {
            var entities = ChangeTracker.Entries<Entity>()
                .Select(e => e.Entity)
                .Where(e => e.DomainEvents.Count > 0)
                .ToList();

            if (entities.Count == 0)
                return;

            var events = entities.SelectMany(e => e.DomainEvents).ToList();
            entities.ForEach(e => e.ClearDomainEvents());

            foreach (var domainEvent in events)
                await publisher.Publish(domainEvent, ct);
        }

        throw new InvalidOperationException("Domain events did not settle after 10 dispatch rounds.");
    }

    private void ApplyAuditRules()
    {
        var now = DateTime.UtcNow;
        var userId = currentUser.UserId;
        var tenantId = SessionTenantId;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>().ToList())
        {
            var entity = entry.Entity;

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entity.TenantId == Guid.Empty)
                        entity.TenantId = tenantId
                            ?? throw new InvalidOperationException($"TenantId is missing for new {entity.GetType().Name}.");
                    entity.CreatedAt = now;
                    entity.CreatedBy ??= userId;
                    break;

                case EntityState.Modified:
                    EnsureSameTenant(entity, tenantId);
                    entity.UpdatedAt = now;
                    entity.UpdatedBy = userId;
                    break;

                case EntityState.Deleted:   // hard delete → soft delete
                    EnsureSameTenant(entity, tenantId);
                    entry.State = EntityState.Modified;
                    entity.IsDeleted = true;

                    // Owned value objects (Address) bhi "Deleted" ho jate hain → columns NULL ho jate. Wapas Unchanged.
                    foreach (var reference in entry.References)
                        if (reference.TargetEntry is { } owned && owned.Metadata.IsOwned())
                            owned.State = EntityState.Unchanged;

                    entity.UpdatedAt = now;
                    entity.UpdatedBy = userId;
                    break;
            }
        }

        _tenant.StampAdded<TenantChildEntity>(ChangeTracker, c => c.TenantId, (c, t) => c.TenantId = t);
    }

    /// <summary>Defense in depth: query filter ke bawajood doosre tenant ka row kabhi update na ho.</summary>
    private static void EnsureSameTenant(AuditableEntity entity, Guid? tenantId)
    {
        if (tenantId is { } current && entity.TenantId != current)
            throw new UnauthorizedAccessException("Cross-tenant write blocked.");
    }
}
