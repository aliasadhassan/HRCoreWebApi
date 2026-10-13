namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Safety;
using HR.Employee.API.Domain.Safety;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Health &amp; safety: incidents (investigation, root cause, authority reporting), corrective actions, checklists aur inspections.
/// Har employee incident report kar sakta hai (anonymous bhi); safety.manage team sab chalati hai; investigator / assignee / inspector
/// apna hissa (SafetyAccess). Gateway /safety/{everything}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/safety")]
public sealed class SafetyController(ISender mediator) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<SafetySummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetSafetySummaryQuery(), ct));

    [HttpGet("lookups")]
    public async Task<ActionResult<SafetyLookupsDto>> Lookups(CancellationToken ct)
        => Ok(await mediator.Send(new GetSafetyLookupsQuery(), ct));

    // ---- Incidents ----

    [HttpGet("incidents")]
    public async Task<ActionResult<IReadOnlyList<IncidentListItemDto>>> Incidents(
        [FromQuery] string? scope = null, [FromQuery] IncidentStatus? status = null, [FromQuery] IncidentType? type = null,
        [FromQuery] SafetySeverity? severity = null, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetIncidentsQuery(scope, status, type, severity, from, to, search), ct));

    [HttpGet("incidents/{id:guid}")]
    public async Task<ActionResult<IncidentDetailDto>> Incident(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetIncidentQuery(id), ct));

    [HttpPost("incidents")]
    public async Task<IActionResult> CreateIncident([FromBody] IncidentRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateIncidentCommand(body), ct);
        return Created($"api/safety/incidents/{id}", new { id });
    }

    [HttpPut("incidents/{id:guid}")]
    public Task<IActionResult> UpdateIncident(Guid id, [FromBody] IncidentRequest body, CancellationToken ct)
        => Run(new UpdateIncidentCommand(id, body), ct);

    [HttpPost("incidents/{id:guid}/status")]
    public Task<IActionResult> MoveIncident(Guid id, [FromBody] SafetyStatusBody body, CancellationToken ct)
        => Run(new MoveIncidentCommand(id, body.Status, body.Note), ct);

    [HttpPost("incidents/{id:guid}/investigator")]
    public Task<IActionResult> Investigator(Guid id, [FromBody] SafetyPersonBody body, CancellationToken ct)
        => Run(new AssignInvestigatorCommand(id, body.EmployeeId), ct);

    [HttpPost("incidents/{id:guid}/root-cause")]
    public Task<IActionResult> RootCause(Guid id, [FromBody] RootCauseBody body, CancellationToken ct)
        => Run(new SetRootCauseCommand(id, body.Category, body.RootCause), ct);

    [HttpPost("incidents/{id:guid}/authority")]
    public Task<IActionResult> Authority(Guid id, [FromBody] AuthorityBody body, CancellationToken ct)
        => Run(new NotifyAuthorityCommand(id, body.NotifiedOn, body.Reference), ct);

    [HttpPost("incidents/{id:guid}/notes")]
    public Task<IActionResult> Note(Guid id, [FromBody] SafetyTextBody body, CancellationToken ct)
        => Run(new AddIncidentNoteCommand(id, body.Text), ct);

    [HttpPost("incidents/{id:guid}/close")]
    public Task<IActionResult> Close(Guid id, [FromBody] SafetyTextBody body, CancellationToken ct)
        => Run(new CloseIncidentCommand(id, body.Text), ct);

    [HttpPost("incidents/{id:guid}/reopen")]
    public Task<IActionResult> Reopen(Guid id, [FromBody] SafetyTextBody body, CancellationToken ct)
        => Run(new ReopenIncidentCommand(id, body.Text), ct);

    // ---- My reports (self-service) ----

    [HttpGet("my-reports")]
    public async Task<ActionResult<IReadOnlyList<MyReportDto>>> MyReports(CancellationToken ct)
        => Ok(await mediator.Send(new GetMyReportsQuery(), ct));

    [HttpGet("my-reports/{id:guid}")]
    public async Task<ActionResult<MyReportDetailDto>> MyReport(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetMyReportQuery(id), ct));

    [HttpPost("my-reports")]
    public async Task<IActionResult> Report([FromBody] ReportIncidentRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new ReportIncidentCommand(body), ct);
        return Created($"api/safety/my-reports/{id}", new { id });
    }

    // ---- Actions ----

    [HttpGet("actions")]
    public async Task<ActionResult<IReadOnlyList<SafetyActionDto>>> Actions(
        [FromQuery] string? scope = null, [FromQuery] ActionStatus? status = null, [FromQuery] string? search = null,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new GetSafetyActionsQuery(scope, status, search), ct));

    [HttpPost("actions")]
    public async Task<IActionResult> CreateAction([FromBody] ActionRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateSafetyActionCommand(body), ct);
        return Created($"api/safety/actions/{id}", new { id });
    }

    [HttpPut("actions/{id:guid}")]
    public Task<IActionResult> UpdateAction(Guid id, [FromBody] ActionRequest body, CancellationToken ct)
        => Run(new UpdateSafetyActionCommand(id, body), ct);

    [HttpPost("actions/{id:guid}/start")]
    public Task<IActionResult> StartAction(Guid id, CancellationToken ct)
        => Run(new StartSafetyActionCommand(id), ct);

    [HttpPost("actions/{id:guid}/complete")]
    public Task<IActionResult> CompleteAction(Guid id, [FromBody] SafetyTextBody body, CancellationToken ct)
        => Run(new CompleteSafetyActionCommand(id, body.Text), ct);

    [HttpPost("actions/{id:guid}/verify")]
    public Task<IActionResult> VerifyAction(Guid id, [FromBody] VerifyBody body, CancellationToken ct)
        => Run(new VerifySafetyActionCommand(id, body.Accepted, body.Note), ct);

    [HttpPost("actions/{id:guid}/cancel")]
    public Task<IActionResult> CancelAction(Guid id, [FromBody] SafetyReasonBody body, CancellationToken ct)
        => Run(new CancelSafetyActionCommand(id, body.Reason), ct);

    // ---- Checklists ----

    [HttpGet("checklists")]
    public async Task<ActionResult<IReadOnlyList<SafetyChecklistDto>>> Checklists(CancellationToken ct)
        => Ok(await mediator.Send(new GetChecklistsQuery(), ct));

    [HttpPost("checklists")]
    public async Task<IActionResult> CreateChecklist([FromBody] ChecklistRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateChecklistCommand(body), ct);
        return Created($"api/safety/checklists/{id}", new { id });
    }

    [HttpPut("checklists/{id:guid}")]
    public Task<IActionResult> UpdateChecklist(Guid id, [FromBody] ChecklistRequest body, CancellationToken ct)
        => Run(new UpdateChecklistCommand(id, body), ct);

    [HttpDelete("checklists/{id:guid}")]
    public Task<IActionResult> DeleteChecklist(Guid id, CancellationToken ct)
        => Run(new DeleteChecklistCommand(id), ct);

    [HttpPost("checklists/samples")]
    public async Task<IActionResult> SampleChecklists(CancellationToken ct)
        => Ok(new { added = await mediator.Send(new AddSampleChecklistsCommand(), ct) });

    // ---- Inspections ----

    [HttpGet("inspections")]
    public async Task<ActionResult<IReadOnlyList<InspectionListItemDto>>> Inspections(
        [FromQuery] string? scope = null, [FromQuery] InspectionStatus? status = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetInspectionsQuery(scope, status), ct));

    [HttpGet("inspections/{id:guid}")]
    public async Task<ActionResult<InspectionDetailDto>> Inspection(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetInspectionQuery(id), ct));

    [HttpPost("inspections")]
    public async Task<IActionResult> Schedule([FromBody] InspectionRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new ScheduleInspectionCommand(body), ct);
        return Created($"api/safety/inspections/{id}", new { id });
    }

    [HttpPut("inspections/{id:guid}")]
    public Task<IActionResult> Reschedule(Guid id, [FromBody] InspectionRequest body, CancellationToken ct)
        => Run(new RescheduleInspectionCommand(id, body), ct);

    [HttpPost("inspections/{id:guid}/cancel")]
    public Task<IActionResult> CancelInspection(Guid id, [FromBody] SafetyReasonBody body, CancellationToken ct)
        => Run(new CancelInspectionCommand(id, body.Reason), ct);

    [HttpPost("inspections/{id:guid}/complete")]
    public async Task<ActionResult<CompleteInspectionResult>> CompleteInspection(
        Guid id, [FromBody] CompleteInspectionRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new CompleteInspectionCommand(id, body), ct));

    private async Task<IActionResult> Run(IRequest command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }
}

public sealed record SafetyStatusBody(IncidentStatus Status, string? Note);
public sealed record SafetyPersonBody(Guid? EmployeeId);
public sealed record RootCauseBody(RootCauseCategory? Category, string? RootCause);
public sealed record AuthorityBody(DateOnly NotifiedOn, string? Reference);
public sealed record SafetyTextBody(string Text);
public sealed record SafetyReasonBody(string? Reason);
public sealed record VerifyBody(bool Accepted, string? Note);
