namespace HR.Payroll.API.Infrastructure.Persistence;

using System.Reflection;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Employees;
using HR.Payroll.API.Domain.Inputs;
using HR.Payroll.API.Domain.Payments;
using HR.Payroll.API.Domain.Runs;
using HR.Payroll.API.Domain.Salaries;
using HR.Payroll.API.Domain.Setup;
using HR.Payroll.API.Domain.Tax;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Employee API wala pattern:
///   - AuditableEntity: TenantId = current AND !IsDeleted
///   - SharedAuditableEntity: (TenantId = current OR TenantId IS NULL) AND !IsDeleted — platform tax rules sab ko dikhein
///   - SaveChanges se pehle domain events → outbox, phir audit + soft delete
/// </summary>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUser currentUser,
    IPublisher publisher) : DbContext(options), IAppDbContext
{
    private static readonly MethodInfo TenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo SharedFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplySharedFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public DbSet<PayrollSettings> PayrollSettings => Set<PayrollSettings>();
    public DbSet<PayGroup> PayGroups => Set<PayGroup>();
    public DbSet<PayPeriod> PayPeriods => Set<PayPeriod>();
    public DbSet<PayComponent> PayComponents => Set<PayComponent>();

    public DbSet<PayrollEmployee> PayrollEmployees => Set<PayrollEmployee>();

    public DbSet<SalaryGrade> SalaryGrades => Set<SalaryGrade>();
    public DbSet<SalaryTemplate> SalaryTemplates => Set<SalaryTemplate>();
    public DbSet<EmployeeSalary> EmployeeSalaries => Set<EmployeeSalary>();

    public DbSet<TaxRegime> TaxRegimes => Set<TaxRegime>();
    public DbSet<ContributionRule> ContributionRules => Set<ContributionRule>();
    public DbSet<EmployeeTaxOpeningBalance> EmployeeTaxOpeningBalances => Set<EmployeeTaxOpeningBalance>();

    public DbSet<PayrollInput> PayrollInputs => Set<PayrollInput>();
    public DbSet<UnpaidLeaveDay> UnpaidLeaveDays => Set<UnpaidLeaveDay>();
    public DbSet<EmployeeLoan> EmployeeLoans => Set<EmployeeLoan>();
    public DbSet<LoanRequest> LoanRequests => Set<LoanRequest>();
    public DbSet<LoanPolicy> LoanPolicies => Set<LoanPolicy>();

    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<LoanRepayment> LoanRepayments => Set<LoanRepayment>();

    public DbSet<PaymentBatch> PaymentBatches => Set<PaymentBatch>();
    public DbSet<Payment> Payments => Set<Payment>();

    private Guid? _tenantOverride;

    /// <summary>
    /// Is scope ka tenant: HTTP request mein JWT wala, background consumer mein message wala (UseTenantAsync).
    /// Query filter, audit rules aur Postgres RLS (app.tenant_id) teeno yahi padhte hain.
    /// </summary>
    internal Guid? SessionTenantId => _tenantOverride ?? currentUser.TenantId;

    /// <summary>Query filter har query pe isay parameter ki tarah padhta hai.</summary>
    private Guid CurrentTenantId => SessionTenantId ?? Guid.Empty;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .ConfigureWarnings(w => w.Ignore(
                CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .AddInterceptors(TenantConnectionInterceptor.Instance);   // RLS: har connection pe app.tenant_id

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HavePrecision(3);
        configurationBuilder.Properties<DateTime?>().HavePrecision(3);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payroll");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;
            if (entityType.IsOwned() || !typeof(Entity).IsAssignableFrom(clrType))
                continue;

            modelBuilder.Entity(clrType).Ignore(nameof(Entity.DomainEvents));

            // Setup entities (PayComponent, PayGroup, PayPeriod, PayrollSettings) ki configuration nahi hai,
            // isliye ConfigureAudit wala IsRowVersion yahan sab pe — RowVersion = Postgres ka xmin
            if (typeof(AuditableBase).IsAssignableFrom(clrType))
                modelBuilder.Entity(clrType).Property(nameof(AuditableBase.RowVersion)).IsRowVersion();

            if (typeof(AuditableEntity).IsAssignableFrom(clrType))
                TenantFilterMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
            else if (typeof(SharedAuditableEntity).IsAssignableFrom(clrType))
                SharedFilterMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
        }

        // Hard-delete table, lekin tenant-scoped
        modelBuilder.Entity<UnpaidLeaveDay>().HasQueryFilter(x => x.TenantId == CurrentTenantId);

        modelBuilder.AddTransactionalOutboxEntities();
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : AuditableEntity
        => modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId && !e.IsDeleted);

    private void ApplySharedFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : SharedAuditableEntity
        => modelBuilder.Entity<TEntity>().HasQueryFilter(e => (e.TenantId == null || e.TenantId == CurrentTenantId) && !e.IsDeleted);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        // EnableRetryOnFailure ke saath user transaction sirf execution strategy ke andar chal sakti hai
        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <summary>
    /// Background consumer (HTTP user nahi): message ka tenant is scope pe lagao — query filter, audit aur RLS sab.
    /// MassTransit inbox consumer se pehle hi connection/transaction khol deta hai, is liye khula connection foran update.
    /// </summary>
    public async Task UseTenantAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant is required.", nameof(tenantId));

        _tenantOverride = tenantId;

        var connection = Database.GetDbConnection();
        if (connection.State == System.Data.ConnectionState.Open)
            await TenantSession.ApplyAsync(connection, tenantId, ct, Database.CurrentTransaction?.GetDbTransaction());
    }

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

        foreach (var entry in ChangeTracker.Entries<AuditableBase>().ToList())
        {
            var entity = entry.Entity;

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entity is AuditableEntity { TenantId: var t } owned && t == Guid.Empty)
                        owned.TenantId = tenantId ?? throw new InvalidOperationException($"TenantId is missing for new {entity.GetType().Name}.");
                    entity.CreatedAt = now;
                    entity.CreatedBy ??= userId;
                    break;

                case EntityState.Modified:
                    EnsureWritable(entity, tenantId);
                    entity.UpdatedAt = now;
                    entity.UpdatedBy = userId;
                    break;

                case EntityState.Deleted:
                    EnsureWritable(entity, tenantId);
                    entry.State = EntityState.Modified;
                    entity.IsDeleted = true;
                    entity.UpdatedAt = now;
                    entity.UpdatedBy = userId;
                    break;
            }
        }

        // Child rows (punch, policy rule, payslip line...) ka apna TenantId: composite FK + RLS isi se check hote hain
        foreach (var entry in ChangeTracker.Entries<TenantChildEntity>())
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                entry.Entity.TenantId = tenantId
                    ?? throw new InvalidOperationException($"TenantId is missing for new {entry.Entity.GetType().Name}.");
    }

    /// <summary>
    /// Defense in depth: doosre tenant ka row nahi, aur tenant user platform rule (TenantId NULL) nahi badal sakta.
    /// Background consumers (tenantId null) pe check nahi — woh message ke tenant se kaam karte hain.
    /// </summary>
    private static void EnsureWritable(AuditableBase entity, Guid? tenantId)
    {
        if (tenantId is not { } current)
            return;

        var rowTenant = entity switch
        {
            AuditableEntity e => e.TenantId,
            SharedAuditableEntity s => s.TenantId,
            _ => null
        };

        if (rowTenant != current)
            throw new UnauthorizedAccessException(rowTenant is null
                ? "Platform-defined rules cannot be changed. Create your own version instead."
                : "Cross-tenant write blocked.");
    }
}
