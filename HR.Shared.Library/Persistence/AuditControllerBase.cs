namespace HR.Shared.Library.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>Employee id → "Naam · Code" (audit list mein "kis employee ka record").</summary>
public sealed class AuditSubject
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

/// <summary>Query string ke filters (/entries?before=..&amp;action=Updated).</summary>
public sealed class AuditEntriesRequest
{
    public DateTime? Before { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public Guid? UserId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public Guid? SubjectEmployeeId { get; set; }
    public AuditAction? Action { get; set; }
    public string? Search { get; set; }
    public int Limit { get; set; } = 50;

    public AuditFilter ToFilter()
        => new(Before, From, To, UserId, EntityType, EntityId, SubjectEmployeeId, Action, Search, Limit);
}

/// <summary>
/// Audit page ke teen endpoints (entries / summary / entity-types) — har service apna Route, permission
/// aur tenant filter wala Logs() deti hai. Gateway: /audit (Employee), /payroll/audit, /audit/access (Identity).
/// </summary>
[ApiController]
[Authorize]
public abstract class AuditControllerBase : ControllerBase
{
    /// <summary>Service ki AuditLogs table (abhi tenant filter ke baghair).</summary>
    protected abstract IQueryable<AuditLog> Source { get; }

    /// <summary>Token ka tenant. Identity pe RLS nahi, isliye filter har query mein yahin se.</summary>
    protected abstract Guid CurrentTenantId();

    /// <summary>Is tenant ke employees (soft delete samet) — null = service ke paas employees nahi (Identity).</summary>
    protected virtual IQueryable<AuditSubject>? Subjects => null;

    /// <summary>Extra permission check (Payroll: tankhwah ka itihaas). Default: class ki [HasPermission] kaafi.</summary>
    protected virtual void EnsureAllowed()
    {
    }

    private IQueryable<AuditLog> Logs()
    {
        EnsureAllowed();
        var tenantId = CurrentTenantId();
        return Source.AsNoTracking().Where(x => x.TenantId == tenantId);
    }

    private Func<IReadOnlyCollection<Guid>, CancellationToken, Task<Dictionary<Guid, string>>>? SubjectNames
        => Subjects is { } subjects
            ? (ids, ct) => subjects.Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct)
            : null;

    [HttpGet("entries")]
    public async Task<ActionResult<AuditPageDto>> Entries([FromQuery] AuditEntriesRequest request, CancellationToken cancellationToken)
        => Ok(await AuditQueries.PageAsync(Logs(), request.ToFilter(), SubjectNames, cancellationToken));

    [HttpGet("summary")]
    public async Task<ActionResult<AuditSummaryDto>> Summary(
        [FromQuery] int days = 30, [FromQuery] int offsetMinutes = 0, CancellationToken cancellationToken = default)
        => Ok(await AuditQueries.SummaryAsync(Logs(), days, offsetMinutes, DateTime.UtcNow, cancellationToken));

    [HttpGet("entity-types")]
    public async Task<ActionResult<IReadOnlyList<AuditCountDto>>> EntityTypes(CancellationToken cancellationToken)
        => Ok(await AuditQueries.EntityTypesAsync(Logs(), cancellationToken));
}
