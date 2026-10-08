namespace HR.Shared.Library.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    /// <summary>Is tenant ki AuditLogs (Identity pe RLS nahi, isliye filter yahin zaroori).</summary>
    protected abstract IQueryable<AuditLog> Logs();

    /// <summary>Employee ids → "Naam · Code". Null = service ke paas employees nahi (Identity).</summary>
    protected virtual Func<IReadOnlyCollection<Guid>, CancellationToken, Task<Dictionary<Guid, string>>>? SubjectNames => null;

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
