namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Audit;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Gateway: /audit/{everything}. Poori company ka itihaas, isliye settings.view (Login activity jaisa).</summary>
[ApiController]
[Authorize]
[Route("api/audit")]
[HasPermission(Permissions.SettingsView)]
public sealed class AuditController(ISender mediator) : ControllerBase
{
    [HttpGet("entries")]
    public async Task<ActionResult<AuditPageDto>> Entries(
        [FromQuery] DateTime? before, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? userId,
        [FromQuery] string? entityType, [FromQuery] Guid? entityId, [FromQuery] Guid? subjectEmployeeId,
        [FromQuery] AuditAction? action, [FromQuery] string? search, [FromQuery] int limit, CancellationToken ct)
        => Ok(await mediator.Send(new GetAuditEntriesQuery(new AuditFilter(
            before, from, to, userId, entityType, entityId, subjectEmployeeId, action, search, limit == 0 ? 50 : limit)), ct));

    [HttpGet("summary")]
    public async Task<ActionResult<AuditSummaryDto>> Summary([FromQuery] int days, [FromQuery] int offsetMinutes, CancellationToken ct)
        => Ok(await mediator.Send(new GetAuditSummaryQuery(days == 0 ? 30 : days, offsetMinutes), ct));

    [HttpGet("entity-types")]
    public async Task<ActionResult<IReadOnlyList<AuditCountDto>>> EntityTypes(CancellationToken ct)
        => Ok(await mediator.Send(new GetAuditEntityTypesQuery(), ct));
}
