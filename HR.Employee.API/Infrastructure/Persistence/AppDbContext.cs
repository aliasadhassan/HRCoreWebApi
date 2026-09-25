namespace HR.Employee.API.Infrastructure.Persistence;

using System.Reflection;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Leaves;
using HR.Employee.API.Domain.Organization;
using MassTransit;
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
    IPublisher publisher) : DbContext(options), IAppDbContext
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

    /// <summary>Query filter har query pe isay parameter ki tarah padhta hai.</summary>
    private Guid CurrentTenantId => currentUser.TenantId ?? Guid.Empty;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        // Child rows parent ke saath hide hon — yahi chahiye, warning ka shor band
        => optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HavePrecision(3);
        configurationBuilder.Properties<DateTime?>().HavePrecision(3);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
        var tenantId = currentUser.TenantId;

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
    }

    /// <summary>Defense in depth: query filter ke bawajood doosre tenant ka row kabhi update na ho.</summary>
    private static void EnsureSameTenant(AuditableEntity entity, Guid? tenantId)
    {
        if (tenantId is { } current && entity.TenantId != current)
            throw new UnauthorizedAccessException("Cross-tenant write blocked.");
    }
}
