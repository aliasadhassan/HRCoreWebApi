namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Policies;
using HR.Employee.API.Domain.Policies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Policies &amp; compliance page. Har employee apni audience ki published policies parhta aur acknowledge karta hai;
/// settings.manage policies + statutory obligations chalata hai, employees.view acknowledgement tracking dekhta hai,
/// settings.view statutory calendar dekhta hai (jaanch handler mein, ComplianceAccess). Gateway /compliance/{everything}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/compliance")]
public sealed class ComplianceController(ISender mediator) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ComplianceSummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetComplianceSummaryQuery(), ct));

    [HttpGet("lookups")]
    public async Task<ActionResult<ComplianceLookupsDto>> Lookups(CancellationToken ct)
        => Ok(await mediator.Send(new GetComplianceLookupsQuery(), ct));

    // ---- Policies ----

    [HttpGet("policies")]
    public async Task<ActionResult<IReadOnlyList<PolicyListItemDto>>> Policies(
        [FromQuery] PolicyStatus? status = null, [FromQuery] PolicyCategory? category = null, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetPoliciesQuery(status, category, search), ct));

    [HttpGet("policies/{id:guid}")]
    public async Task<ActionResult<PolicyDto>> Policy(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetPolicyQuery(id), ct));

    [HttpGet("policies/{id:guid}/versions/{number:int}")]
    public async Task<ActionResult<PolicyVersionContentDto>> Version(Guid id, int number, CancellationToken ct)
        => Ok(await mediator.Send(new GetPolicyVersionQuery(id, number), ct));

    [HttpGet("policies/{id:guid}/acknowledgements")]
    public async Task<ActionResult<IReadOnlyList<AckPersonDto>>> Acknowledgements(
        Guid id, [FromQuery] AckState? state = null, [FromQuery] Guid? departmentId = null, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetPolicyAcknowledgementsQuery(id, state, departmentId, search), ct));

    [HttpPost("policies")]
    public async Task<IActionResult> CreatePolicy([FromBody] SavePolicyRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreatePolicyCommand(body), ct);
        return Created($"api/compliance/policies/{id}", new { id });
    }

    [HttpPut("policies/{id:guid}")]
    public Task<IActionResult> UpdatePolicy(Guid id, [FromBody] SavePolicyRequest body, CancellationToken ct)
        => Run(new UpdatePolicyCommand(id, body), ct);

    [HttpPost("policies/{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id, [FromBody] PublishPolicyBody body, CancellationToken ct)
        => Run(new PublishPolicyCommand(id, body.ChangeSummary, body.Reacknowledge, body.EffectiveOn), ct);

    [HttpPost("policies/{id:guid}/archive")]
    public Task<IActionResult> Archive(Guid id, CancellationToken ct) => Run(new ArchivePolicyCommand(id), ct);

    [HttpPost("policies/{id:guid}/restore")]
    public Task<IActionResult> Restore(Guid id, CancellationToken ct) => Run(new RestorePolicyCommand(id), ct);

    [HttpPost("policies/{id:guid}/reviewed")]
    public Task<IActionResult> Reviewed(Guid id, CancellationToken ct) => Run(new MarkPolicyReviewedCommand(id), ct);

    [HttpPost("policies/{id:guid}/acknowledge")]
    public Task<IActionResult> Acknowledge(Guid id, CancellationToken ct) => Run(new AcknowledgePolicyCommand(id), ct);

    [HttpDelete("policies/{id:guid}")]
    public Task<IActionResult> DeletePolicy(Guid id, CancellationToken ct) => Run(new DeletePolicyCommand(id), ct);

    [HttpPost("policies/starter")]
    public async Task<IActionResult> AddStarter(CancellationToken ct)
        => Ok(new { added = await mediator.Send(new AddStarterPoliciesCommand(), ct) });

    // ---- Statutory obligations ----

    [HttpGet("obligations")]
    public async Task<ActionResult<IReadOnlyList<ObligationDto>>> Obligations(CancellationToken ct)
        => Ok(await mediator.Send(new GetObligationsQuery(), ct));

    [HttpGet("obligations/{id:guid}")]
    public async Task<ActionResult<ObligationDetailDto>> Obligation(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetObligationQuery(id), ct));

    [HttpPost("obligations")]
    public async Task<IActionResult> CreateObligation([FromBody] SaveObligationRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateObligationCommand(body), ct);
        return Created($"api/compliance/obligations/{id}", new { id });
    }

    [HttpPut("obligations/{id:guid}")]
    public Task<IActionResult> UpdateObligation(Guid id, [FromBody] SaveObligationRequest body, CancellationToken ct)
        => Run(new UpdateObligationCommand(id, body), ct);

    [HttpDelete("obligations/{id:guid}")]
    public Task<IActionResult> DeleteObligation(Guid id, CancellationToken ct) => Run(new DeleteObligationCommand(id), ct);

    [HttpPost("obligations/{id:guid}/filings")]
    public async Task<IActionResult> RecordFiling(Guid id, [FromBody] RecordFilingRequest body, CancellationToken ct)
    {
        var filingId = await mediator.Send(new RecordFilingCommand(id, body), ct);
        return Created($"api/compliance/obligations/{id}", new { id = filingId });
    }

    [HttpDelete("obligations/{id:guid}/filings/{filingId:guid}")]
    public Task<IActionResult> UndoFiling(Guid id, Guid filingId, CancellationToken ct) => Run(new UndoFilingCommand(id, filingId), ct);

    private async Task<IActionResult> Run(IRequest command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }
}

public sealed record PublishPolicyBody(string? ChangeSummary, bool Reacknowledge, DateOnly? EffectiveOn);
