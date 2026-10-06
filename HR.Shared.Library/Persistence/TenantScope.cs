namespace HR.Shared.Library.Persistence;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Ek DbContext scope ka tenant: HTTP request mein JWT wala, background consumer mein message wala (UseAsync).
/// Query filter, audit rules aur Postgres RLS (app.tenant_id) teeno yahi padhte hain.
/// </summary>
public sealed class TenantScope(Func<Guid?> fromUser)
{
    private Guid? _override;

    public Guid? TenantId => _override ?? fromUser();

    /// <summary>Query filter ke liye: tenant nahi to Guid.Empty (koi row match nahi).</summary>
    public Guid FilterId => TenantId ?? Guid.Empty;

    /// <summary>
    /// Consumer: message ka tenant lagao. MassTransit inbox consumer se pehle hi connection/transaction khol deta hai,
    /// is liye khula connection foran update hota hai.
    /// </summary>
    public Task UseAsync(DatabaseFacade database, Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant is required.", nameof(tenantId));

        _override = tenantId;
        return TenantSession.ApplyIfOpenAsync(database, tenantId, ct);
    }

    /// <summary>Naye child rows (punch, policy rule, payslip line...) ko session ka TenantId do: composite FK + RLS isi se check hote hain.</summary>
    public void StampAdded<TChild>(ChangeTracker changeTracker, Func<TChild, Guid> get, Action<TChild, Guid> set)
        where TChild : class
    {
        foreach (var entry in changeTracker.Entries<TChild>())
            if (entry.State == EntityState.Added && get(entry.Entity) == Guid.Empty)
                set(entry.Entity, TenantId
                    ?? throw new InvalidOperationException($"TenantId is missing for new {entry.Entity.GetType().Name}."));
    }
}
