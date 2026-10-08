namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Audit;
using HR.Shared.Library.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Gateway: /payroll/audit/{everything}. Permission handler mein (payroll.view.all ya payroll.approve).</summary>
[ApiController]
[Authorize]
[Route("api/payroll/audit")]
public sealed class PayrollAuditController(ISender mediator) : ControllerBase
{
    [HttpGet("entries")]
    public async Task<ActionResult<AuditPageDto>> Entries(
        [FromQuery] DateTime? before, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? userId,
        [FromQuery] string? entityType, [FromQuery] Guid? entityId, [FromQuery] Guid? subjectEmployeeId,
        [FromQuery] AuditAction? action, [FromQuery] string? search, [FromQuery] int limit, CancellationToken ct)
        => Ok(await mediator.Send(new GetPayrollAuditEntriesQuery(new AuditFilter(
            before, from, to, userId, entityType, entityId, subjectEmployeeId, action, search, limit == 0 ? 50 : limit)), ct));

    [HttpGet("summary")]
    public async Task<ActionResult<AuditSummaryDto>> Summary([FromQuery] int days, [FromQuery] int offsetMinutes, CancellationToken ct)
        => Ok(await mediator.Send(new GetPayrollAuditSummaryQuery(days == 0 ? 30 : days, offsetMinutes), ct));

    [HttpGet("entity-types")]
    public async Task<ActionResult<IReadOnlyList<AuditCountDto>>> EntityTypes(CancellationToken ct)
        => Ok(await mediator.Send(new GetPayrollAuditEntityTypesQuery(), ct));
}
