using HR.Identity.API.Data;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Controllers;

/// <summary>
/// Audit page ka "access" hissa: users, roles, permissions, company settings ke badlaav. Sirf parhna.
/// Gateway: /audit/access/{everything} → yahan. Identity pe RLS nahi, isliye tenant filter yahin.
/// </summary>
[ApiController]
[Route("api/audit")]
[HasPermission(Permissions.SettingsView)]
public sealed class AuditController(AppDbContext db) : ControllerBase
{
    private IQueryable<Models.AuditLog> Logs()
    {
        var tenantId = User.GetTenantId() ?? throw new UnauthorizedAccessException("Tenant missing in token.");
        return db.AuditLogs.AsNoTracking().Where(x => x.TenantId == tenantId);
    }

    [HttpGet("entries")]
    public async Task<ActionResult<AuditPageDto>> Entries(
        [FromQuery] DateTime? before, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? userId,
        [FromQuery] string? entityType, [FromQuery] Guid? entityId, [FromQuery] Guid? subjectEmployeeId,
        [FromQuery] AuditAction? action, [FromQuery] string? search, [FromQuery] int limit, CancellationToken ct)
        => Ok(await AuditQueries.PageAsync(Logs(),
            new AuditFilter(before, from, to, userId, entityType, entityId, subjectEmployeeId, action, search, limit == 0 ? 50 : limit),
            null, ct));

    [HttpGet("summary")]
    public async Task<ActionResult<AuditSummaryDto>> Summary([FromQuery] int days, [FromQuery] int offsetMinutes, CancellationToken ct)
        => Ok(await AuditQueries.SummaryAsync(Logs(), days == 0 ? 30 : days, offsetMinutes, DateTime.UtcNow, ct));

    [HttpGet("entity-types")]
    public async Task<ActionResult<IReadOnlyList<AuditCountDto>>> EntityTypes(CancellationToken ct)
        => Ok(await AuditQueries.EntityTypesAsync(Logs(), ct));
}
