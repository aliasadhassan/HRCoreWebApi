namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Relations;
using HR.Employee.API.Domain.Relations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Employee relations: confidential cases (complaints, grievances, harassment, disciplinary). Har employee "concern" utha sakta hai;
/// relations.manage team cases chalati hai, aur case-level members (owner / investigator / viewer) hi restricted case dekhte hain
/// (RelationsAccess). Gateway /relations/{everything}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/relations")]
public sealed class RelationsController(ISender mediator) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<RelationsSummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetRelationsSummaryQuery(), ct));

    [HttpGet("lookups")]
    public async Task<ActionResult<RelationsLookupsDto>> Lookups(CancellationToken ct)
        => Ok(await mediator.Send(new GetRelationsLookupsQuery(), ct));

    // ---- Cases (team) ----

    [HttpGet("cases")]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> Cases(
        [FromQuery] string? scope = null, [FromQuery] CasePhase? phase = null, [FromQuery] CaseType? type = null,
        [FromQuery] CaseSeverity? severity = null, [FromQuery] bool includeClosed = false, [FromQuery] string? search = null,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new GetCasesQuery(scope, phase, type, severity, includeClosed, search), ct));

    [HttpGet("cases/{id:guid}")]
    public async Task<ActionResult<CaseDetailDto>> Case(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetCaseQuery(id), ct));

    [HttpGet("cases/{id:guid}/access")]
    public async Task<ActionResult<IReadOnlyList<CaseAccessLogDto>>> AccessLog(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetCaseAccessLogQuery(id), ct));

    [HttpPost("cases")]
    public async Task<IActionResult> CreateCase([FromBody] CreateCaseRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateCaseCommand(body), ct);
        return Created($"api/relations/cases/{id}", new { id });
    }

    [HttpPut("cases/{id:guid}")]
    public Task<IActionResult> UpdateCase(Guid id, [FromBody] UpdateCaseRequest body, CancellationToken ct)
        => Run(new UpdateCaseCommand(id, body), ct);

    [HttpPost("cases/{id:guid}/stage")]
    public Task<IActionResult> Move(Guid id, [FromBody] MoveCaseBody body, CancellationToken ct)
        => Run(new MoveCaseCommand(id, body.StageId, body.Note), ct);

    [HttpPost("cases/{id:guid}/close")]
    public Task<IActionResult> Close(Guid id, [FromBody] CloseCaseBody body, CancellationToken ct)
        => Run(new CloseCaseCommand(id, body.StageId, body.Outcome, body.Action, body.Summary), ct);

    [HttpPost("cases/{id:guid}/reopen")]
    public Task<IActionResult> Reopen(Guid id, [FromBody] ReopenCaseBody body, CancellationToken ct)
        => Run(new ReopenCaseCommand(id, body.StageId, body.Reason), ct);

    [HttpPost("cases/{id:guid}/deadline")]
    public Task<IActionResult> Deadline(Guid id, [FromBody] DeadlineBody body, CancellationToken ct)
        => Run(new SetCaseDeadlineCommand(id, body.DueOn, body.Reason), ct);

    [HttpPost("cases/{id:guid}/escalate")]
    public Task<IActionResult> Escalate(Guid id, [FromBody] EscalateBody body, CancellationToken ct)
        => Run(new EscalateCaseCommand(id, body.Reason, body.ToEmployeeId), ct);

    [HttpPost("cases/{id:guid}/take")]
    public Task<IActionResult> Take(Guid id, CancellationToken ct)
        => Run(new TakeCaseCommand(id), ct);

    [HttpPost("cases/{id:guid}/owner")]
    public Task<IActionResult> Owner(Guid id, [FromBody] PersonBody body, CancellationToken ct)
        => Run(new ChangeCaseOwnerCommand(id, body.EmployeeId), ct);

    [HttpPost("cases/{id:guid}/members")]
    public Task<IActionResult> AddMember(Guid id, [FromBody] MemberBody body, CancellationToken ct)
        => Run(new AddCaseMemberCommand(id, body.EmployeeId, body.Role), ct);

    [HttpDelete("cases/{id:guid}/members/{employeeId:guid}")]
    public Task<IActionResult> RemoveMember(Guid id, Guid employeeId, CancellationToken ct)
        => Run(new RemoveCaseMemberCommand(id, employeeId), ct);

    [HttpPost("cases/{id:guid}/parties")]
    public async Task<IActionResult> AddParty(Guid id, [FromBody] CasePartyInput body, CancellationToken ct)
        => Ok(new { id = await mediator.Send(new AddCasePartyCommand(id, body), ct) });

    [HttpDelete("cases/{id:guid}/parties/{partyId:guid}")]
    public Task<IActionResult> RemoveParty(Guid id, Guid partyId, CancellationToken ct)
        => Run(new RemoveCasePartyCommand(id, partyId), ct);

    [HttpPost("cases/{id:guid}/notes")]
    public Task<IActionResult> Note(Guid id, [FromBody] NoteBody body, CancellationToken ct)
        => Run(new AddCaseNoteCommand(id, body.Text, body.Shared), ct);

    [HttpPost("cases/{id:guid}/evidence")]
    public async Task<IActionResult> AddEvidence(Guid id, [FromBody] EvidenceBody body, CancellationToken ct)
        => Ok(new { id = await mediator.Send(new AddCaseEvidenceCommand(id, body.Title, body.Url, body.Note, body.Restricted), ct) });

    [HttpDelete("cases/{id:guid}/evidence/{evidenceId:guid}")]
    public Task<IActionResult> RemoveEvidence(Guid id, Guid evidenceId, CancellationToken ct)
        => Run(new RemoveCaseEvidenceCommand(id, evidenceId), ct);

    [HttpPost("cases/{id:guid}/break-glass")]
    public Task<IActionResult> BreakGlass(Guid id, [FromBody] ReasonBody body, CancellationToken ct)
        => Run(new BreakGlassCommand(id, body.Reason), ct);

    // ---- Concerns (jo employee ne khud uthaye) ----

    [HttpGet("concerns")]
    public async Task<ActionResult<IReadOnlyList<ConcernListItemDto>>> Concerns(CancellationToken ct)
        => Ok(await mediator.Send(new GetConcernsQuery(), ct));

    [HttpGet("concerns/{id:guid}")]
    public async Task<ActionResult<ConcernDetailDto>> Concern(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetConcernQuery(id), ct));

    [HttpPost("concerns")]
    public async Task<IActionResult> RaiseConcern([FromBody] RaiseConcernRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new RaiseConcernCommand(body), ct);
        return Created($"api/relations/concerns/{id}", new { id });
    }

    [HttpPost("concerns/{id:guid}/messages")]
    public Task<IActionResult> ConcernMessage(Guid id, [FromBody] NoteBody body, CancellationToken ct)
        => Run(new ConcernMessageCommand(id, body.Text), ct);

    [HttpPost("concerns/{id:guid}/evidence")]
    public async Task<IActionResult> ConcernEvidence(Guid id, [FromBody] EvidenceBody body, CancellationToken ct)
        => Ok(new { id = await mediator.Send(new ConcernEvidenceCommand(id, body.Title, body.Url, body.Note), ct) });

    // ---- Stages ----

    [HttpGet("stages")]
    public async Task<ActionResult<IReadOnlyList<CaseStageDto>>> Stages(CancellationToken ct)
        => Ok(await mediator.Send(new GetCaseStagesQuery(), ct));

    [HttpPost("stages")]
    public async Task<IActionResult> CreateStage([FromBody] SaveStageRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateStageCommand(body), ct);
        return Created($"api/relations/stages/{id}", new { id });
    }

    [HttpPut("stages/{id:guid}")]
    public Task<IActionResult> UpdateStage(Guid id, [FromBody] SaveStageRequest body, CancellationToken ct)
        => Run(new UpdateStageCommand(id, body), ct);

    [HttpDelete("stages/{id:guid}")]
    public Task<IActionResult> DeleteStage(Guid id, CancellationToken ct)
        => Run(new DeleteStageCommand(id), ct);

    [HttpPost("stages/defaults")]
    public async Task<IActionResult> DefaultStages(CancellationToken ct)
        => Ok(new { added = await mediator.Send(new AddDefaultStagesCommand(), ct) });

    private async Task<IActionResult> Run(IRequest command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }
}

public sealed record MoveCaseBody(Guid StageId, string? Note);
public sealed record CloseCaseBody(Guid? StageId, CaseOutcome Outcome, CaseAction Action, string Summary);
public sealed record ReopenCaseBody(Guid? StageId, string Reason);
public sealed record DeadlineBody(DateOnly? DueOn, string? Reason);
public sealed record EscalateBody(string Reason, Guid? ToEmployeeId);
public sealed record PersonBody(Guid EmployeeId);
public sealed record MemberBody(Guid EmployeeId, CaseMemberRole Role);
public sealed record NoteBody(string Text, bool Shared);
public sealed record EvidenceBody(string Title, string Url, string? Note, bool Restricted);
public sealed record ReasonBody(string Reason);
