using HR.Identity.API.Data;
using HR.Identity.API.Models;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Services;

/// <summary>
/// License checks (audit M4). Login/SSO/refresh pe expiry, invite/activate pe seat limit.
/// Tenant ki current row hi na ho to rok-tok nahi (purane tenants; 08 script sab ko row de deta hai).
/// </summary>
public sealed class SubscriptionService(AppDbContext db)
{
    public Task<TenantSubscription?> GetCurrentAsync(Guid tenantId, CancellationToken ct = default) =>
        db.TenantSubscriptions.AsNoTracking()
          .Where(s => s.TenantId == tenantId && TenantSubscription.CurrentStatuses.Contains(s.Status))
          .OrderByDescending(s => s.StartDate)
          .FirstOrDefaultAsync(ct);

    public async Task<bool> HasAccessAsync(Guid tenantId, CancellationToken ct = default)
    {
        var current = await GetCurrentAsync(tenantId, ct);
        return current is null || current.AllowsAccessOn(Today);
    }

    /// <summary>Active users (invited bhi) seat lete hain.</summary>
    public Task<int> SeatsUsedAsync(Guid tenantId, CancellationToken ct = default) =>
        db.Users.CountAsync(u => u.TenantId == tenantId && u.IsActive, ct);

    public async Task<bool> HasFreeSeatAsync(Guid tenantId, CancellationToken ct = default)
    {
        var limit = (await GetCurrentAsync(tenantId, ct))?.SeatLimit;
        return limit is null || await SeatsUsedAsync(tenantId, ct) < limit;
    }

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
