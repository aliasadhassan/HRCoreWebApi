namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Rewards;
using HR.Payroll.API.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Gateway: /payroll/rewards/... — Benefits &amp; rewards page. Permissions handler mein:
/// me + apni enrolment request: har employee. Lists: payroll.view.all / payroll.run / payroll.approve.
/// Tajweez + enrolment: payroll.run. Increment / bonus approve: payroll.approve. Plans: settings.manage.
/// </summary>
[ApiController]
[Authorize]
[Route("api/payroll/rewards")]
public sealed class RewardsController(ISender mediator) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<RewardsSummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetRewardsSummaryQuery(), ct));

    [HttpGet("lookups")]
    public async Task<ActionResult<RewardsLookupsDto>> Lookups(CancellationToken ct)
        => Ok(await mediator.Send(new GetRewardsLookupsQuery(), ct));

    // ---- Employee ----

    [HttpGet("me")]
    public async Task<ActionResult<MyRewardsDto>> Me(CancellationToken ct)
        => Ok(await mediator.Send(new GetMyRewardsQuery(), ct));

    [HttpPost("me/enrolments")]
    public async Task<IActionResult> RequestEnrolment([FromBody] RequestEnrolmentCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/payroll/rewards/enrolments/{id}", new { id });
    }

    [HttpPost("me/enrolments/{id:guid}/cancel")]
    public async Task<IActionResult> CancelEnrolmentRequest(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelEnrolmentRequestCommand(id), ct);
        return NoContent();
    }

    // ---- Plans ----

    [HttpGet("plans")]
    public async Task<ActionResult<IReadOnlyList<BenefitPlanDto>>> Plans(CancellationToken ct)
        => Ok(await mediator.Send(new GetBenefitPlansQuery(), ct));

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] BenefitPlanBody body, CancellationToken ct)
    {
        var id = await mediator.Send(body.ToCommand(null), ct);
        return Created($"api/payroll/rewards/plans/{id}", new { id });
    }

    [HttpPut("plans/{id:guid}")]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] BenefitPlanBody body, CancellationToken ct)
    {
        await mediator.Send(body.ToCommand(id), ct);
        return NoContent();
    }

    [HttpDelete("plans/{id:guid}")]
    public async Task<IActionResult> DeletePlan(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteBenefitPlanCommand(id), ct);
        return NoContent();
    }

    // ---- Enrolments ----

    [HttpGet("enrolments")]
    public async Task<ActionResult<IReadOnlyList<EnrolmentDto>>> Enrolments(
        [FromQuery] EnrolmentStatus? status, [FromQuery] Guid? planId, [FromQuery] Guid? employeeId, CancellationToken ct)
        => Ok(await mediator.Send(new GetEnrolmentsQuery(status, planId, employeeId), ct));

    [HttpPost("enrolments")]
    public async Task<IActionResult> Enrol([FromBody] EnrolEmployeeCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/payroll/rewards/enrolments/{id}", new { id });
    }

    [HttpPost("enrolments/{id:guid}/approve")]
    public async Task<IActionResult> ApproveEnrolment(Guid id, [FromBody] ApproveEnrolmentBody body, CancellationToken ct)
    {
        await mediator.Send(new ApproveEnrolmentCommand(id, body.StartDate, body.Dependents, body.Note), ct);
        return NoContent();
    }

    [HttpPost("enrolments/{id:guid}/reject")]
    public async Task<IActionResult> RejectEnrolment(Guid id, [FromBody] RewardNoteBody body, CancellationToken ct)
    {
        await mediator.Send(new RejectEnrolmentCommand(id, body.Note ?? string.Empty), ct);
        return NoContent();
    }

    [HttpPost("enrolments/{id:guid}/end")]
    public async Task<IActionResult> EndEnrolment(Guid id, [FromBody] EndEnrolmentBody body, CancellationToken ct)
    {
        await mediator.Send(new EndEnrolmentCommand(id, body.EndDate, body.Note), ct);
        return NoContent();
    }

    [HttpPut("enrolments/{id:guid}/dependents")]
    public async Task<IActionResult> ChangeDependents(Guid id, [FromBody] DependentsBody body, CancellationToken ct)
    {
        await mediator.Send(new ChangeEnrolmentDependentsCommand(id, body.Dependents), ct);
        return NoContent();
    }

    // ---- Increments ----

    [HttpGet("salaries")]
    public async Task<ActionResult<IReadOnlyList<SalaryLineDto>>> Salaries(CancellationToken ct)
        => Ok(await mediator.Send(new GetSalaryLinesQuery(), ct));

    [HttpGet("revisions")]
    public async Task<ActionResult<IReadOnlyList<SalaryRevisionDto>>> Revisions(
        [FromQuery] SalaryRevisionStatus? status, [FromQuery] Guid? employeeId, CancellationToken ct)
        => Ok(await mediator.Send(new GetSalaryRevisionsQuery(status, employeeId), ct));

    [HttpPost("revisions")]
    public async Task<IActionResult> ProposeRevision([FromBody] ProposeSalaryRevisionCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/payroll/rewards/revisions/{id}", new { id });
    }

    [HttpPost("revisions/batch")]
    public async Task<ActionResult<BatchResultDto>> ProposeRevisions([FromBody] ProposeSalaryRevisionsBatchCommand command, CancellationToken ct)
        => Ok(await mediator.Send(command, ct));

    [HttpPut("revisions/{id:guid}")]
    public async Task<IActionResult> UpdateRevision(Guid id, [FromBody] RevisionBody body, CancellationToken ct)
    {
        await mediator.Send(new UpdateSalaryRevisionCommand(id, body.Reason, body.ProposedAmount, body.EffectiveFrom, body.NewTitle, body.Justification), ct);
        return NoContent();
    }

    [HttpPost("revisions/approve")]
    public async Task<ActionResult<BatchResultDto>> ApproveRevisions([FromBody] ApproveSalaryRevisionsCommand command, CancellationToken ct)
        => Ok(await mediator.Send(command, ct));

    [HttpPost("revisions/{id:guid}/reject")]
    public async Task<IActionResult> RejectRevision(Guid id, [FromBody] RewardNoteBody body, CancellationToken ct)
    {
        await mediator.Send(new RejectSalaryRevisionCommand(id, body.Note ?? string.Empty), ct);
        return NoContent();
    }

    [HttpPost("revisions/{id:guid}/cancel")]
    public async Task<IActionResult> CancelRevision(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelSalaryRevisionCommand(id), ct);
        return NoContent();
    }

    // ---- Bonuses ----

    [HttpGet("bonuses")]
    public async Task<ActionResult<IReadOnlyList<BonusDto>>> Bonuses(
        [FromQuery] BonusStatus? status, [FromQuery] BonusType? type, [FromQuery] Guid? employeeId, CancellationToken ct)
        => Ok(await mediator.Send(new GetBonusesQuery(status, type, employeeId), ct));

    [HttpPost("bonuses")]
    public async Task<IActionResult> ProposeBonus([FromBody] ProposeBonusCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/payroll/rewards/bonuses/{id}", new { id });
    }

    [HttpPost("bonuses/batch")]
    public async Task<ActionResult<BatchResultDto>> ProposeBonuses([FromBody] ProposeBonusBatchCommand command, CancellationToken ct)
        => Ok(await mediator.Send(command, ct));

    [HttpPut("bonuses/{id:guid}")]
    public async Task<IActionResult> UpdateBonus(Guid id, [FromBody] BonusBody body, CancellationToken ct)
    {
        await mediator.Send(new UpdateBonusCommand(id, body.BonusType, body.Title, body.Amount, body.PayComponentId, body.Reason), ct);
        return NoContent();
    }

    [HttpPost("bonuses/approve")]
    public async Task<ActionResult<BatchResultDto>> ApproveBonuses([FromBody] ApproveBonusesCommand command, CancellationToken ct)
        => Ok(await mediator.Send(command, ct));

    [HttpPost("bonuses/{id:guid}/reject")]
    public async Task<IActionResult> RejectBonus(Guid id, [FromBody] RewardNoteBody body, CancellationToken ct)
    {
        await mediator.Send(new RejectBonusCommand(id, body.Note ?? string.Empty), ct);
        return NoContent();
    }

    [HttpPost("bonuses/{id:guid}/cancel")]
    public async Task<IActionResult> CancelBonus(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelBonusCommand(id), ct);
        return NoContent();
    }

    [HttpPost("bonuses/{id:guid}/paid")]
    public async Task<IActionResult> MarkBonusPaid(Guid id, CancellationToken ct)
    {
        await mediator.Send(new MarkBonusPaidCommand(id), ct);
        return NoContent();
    }
}

public sealed record BenefitPlanBody(
    string Name, BenefitType BenefitType, string? Provider, string? Description, string? CurrencyCode,
    decimal EmployerMonthlyCost, decimal EmployeeMonthlyCost, decimal DependentMonthlyCost, short MaxDependents,
    Guid? DeductionComponentId, bool OpenForRequests, bool IsActive, short SortOrder)
{
    public SaveBenefitPlanCommand ToCommand(Guid? id) => new(
        id, Name, BenefitType, Provider, Description, CurrencyCode, EmployerMonthlyCost, EmployeeMonthlyCost, DependentMonthlyCost,
        MaxDependents, DeductionComponentId, OpenForRequests, IsActive, SortOrder);
}

public sealed record ApproveEnrolmentBody(DateOnly StartDate, short? Dependents, string? Note);
public sealed record EndEnrolmentBody(DateOnly EndDate, string? Note);
public sealed record DependentsBody(short Dependents);
public sealed record RewardNoteBody(string? Note);
public sealed record RevisionBody(SalaryChangeReason Reason, decimal ProposedAmount, DateOnly EffectiveFrom, string? NewTitle, string? Justification);
public sealed record BonusBody(BonusType BonusType, string Title, decimal Amount, Guid PayComponentId, string? Reason);
