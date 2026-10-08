namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Performance;
using HR.Employee.API.Domain.Performance;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Performance page: cycles (HR), reviews, goals + check-ins, my performance.
/// Managers ke paas koi khaas permission nahi hoti — team / reviewer ki jaanch handler mein (PerformanceAccess).
/// Gateway /performance/{everything}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/performance")]
public sealed class PerformanceController(ISender mediator) : ControllerBase
{
    // ---- Overview ----

    [HttpGet("summary")]
    public async Task<ActionResult<PerformanceSummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetPerformanceSummaryQuery(), ct));

    [HttpGet("my")]
    public async Task<ActionResult<MyPerformanceDto>> Mine(CancellationToken ct)
        => Ok(await mediator.Send(new GetMyPerformanceQuery(), ct));

    [HttpGet("people")]
    public async Task<ActionResult<IReadOnlyList<PerformancePersonDto>>> People(CancellationToken ct)
        => Ok(await mediator.Send(new GetPerformancePeopleQuery(), ct));

    // ---- Cycles ----

    [HttpGet("cycles")]
    public async Task<ActionResult<IReadOnlyList<ReviewCycleDto>>> Cycles(CancellationToken ct)
        => Ok(await mediator.Send(new GetReviewCyclesQuery(), ct));

    [HttpPost("cycles")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> CreateCycle([FromBody] SaveReviewCycleRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateReviewCycleCommand(body), ct);
        return Created($"api/performance/cycles/{id}", new { id });
    }

    [HttpPut("cycles/{id:guid}")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> UpdateCycle(Guid id, [FromBody] SaveReviewCycleRequest body, CancellationToken ct)
    {
        await mediator.Send(new UpdateReviewCycleCommand(id, body), ct);
        return NoContent();
    }

    [HttpDelete("cycles/{id:guid}")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> DeleteCycle(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteReviewCycleCommand(id), ct);
        return NoContent();
    }

    [HttpPost("cycles/{id:guid}/launch")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<ActionResult<LaunchResultDto>> Launch(Guid id, [FromBody] LaunchCycleRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new LaunchReviewCycleCommand(id, body), ct));

    [HttpPost("cycles/{id:guid}/reviews")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<ActionResult<LaunchResultDto>> AddReviews(Guid id, [FromBody] AddReviewsRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new AddReviewsCommand(id, body), ct));

    [HttpPost("cycles/{id:guid}/close")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CloseReviewCycleCommand(id), ct);
        return NoContent();
    }

    // ---- Reviews ----

    [HttpGet("reviews")]
    public async Task<ActionResult<IReadOnlyList<ReviewListItemDto>>> Reviews(
        [FromQuery] Guid? cycleId, [FromQuery] ReviewStatus? status, [FromQuery] bool? overdue, [FromQuery] string? search, CancellationToken ct)
        => Ok(await mediator.Send(new GetReviewsQuery(cycleId, status, overdue, search), ct));

    [HttpGet("reviews/{id:guid}")]
    public async Task<ActionResult<ReviewDto>> Review(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetReviewQuery(id), ct));

    [HttpPut("reviews/{id:guid}/self")]
    public async Task<IActionResult> SaveSelf(Guid id, [FromBody] SaveSelfReviewRequest body, CancellationToken ct)
    {
        await mediator.Send(new SaveSelfReviewCommand(id, body), ct);
        return NoContent();
    }

    [HttpPut("reviews/{id:guid}/manager")]
    public async Task<IActionResult> SaveManager(Guid id, [FromBody] SaveManagerReviewRequest body, CancellationToken ct)
    {
        await mediator.Send(new SaveManagerReviewCommand(id, body), ct);
        return NoContent();
    }

    [HttpPost("reviews/{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, [FromBody] AcknowledgeReviewBody? body, CancellationToken ct)
    {
        await mediator.Send(new AcknowledgeReviewCommand(id, body?.Comment), ct);
        return NoContent();
    }

    [HttpPost("reviews/{id:guid}/reopen")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Reopen(Guid id, [FromQuery] bool selfReview, CancellationToken ct)
    {
        await mediator.Send(new ReopenReviewCommand(id, selfReview), ct);
        return NoContent();
    }

    [HttpPut("reviews/{id:guid}/reviewer")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> ChangeReviewer(Guid id, [FromBody] ChangeReviewerBody body, CancellationToken ct)
    {
        await mediator.Send(new ChangeReviewerCommand(id, body.ReviewerEmployeeId), ct);
        return NoContent();
    }

    [HttpDelete("reviews/{id:guid}")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> DeleteReview(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteReviewCommand(id), ct);
        return NoContent();
    }

    // ---- Goals ----

    [HttpGet("goals")]
    public async Task<ActionResult<IReadOnlyList<GoalListItemDto>>> Goals(
        [FromQuery] Guid? employeeId, [FromQuery] Guid? cycleId, [FromQuery] GoalStatus? status, [FromQuery] bool? overdue,
        [FromQuery] string? search, CancellationToken ct)
        => Ok(await mediator.Send(new GetGoalsQuery(employeeId, cycleId, status, overdue, search), ct));

    [HttpGet("goals/{id:guid}")]
    public async Task<ActionResult<GoalDto>> Goal(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetGoalQuery(id), ct));

    [HttpPost("goals")]
    public async Task<IActionResult> CreateGoal([FromBody] SaveGoalRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateGoalCommand(body), ct);
        return Created($"api/performance/goals/{id}", new { id });
    }

    [HttpPut("goals/{id:guid}")]
    public async Task<IActionResult> UpdateGoal(Guid id, [FromBody] SaveGoalRequest body, CancellationToken ct)
    {
        await mediator.Send(new UpdateGoalCommand(id, body), ct);
        return NoContent();
    }

    [HttpDelete("goals/{id:guid}")]
    public async Task<IActionResult> DeleteGoal(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteGoalCommand(id), ct);
        return NoContent();
    }

    [HttpPost("goals/{id:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid id, [FromBody] GoalCheckInRequest body, CancellationToken ct)
    {
        await mediator.Send(new CheckInGoalCommand(id, body), ct);
        return NoContent();
    }

    [HttpPost("goals/{id:guid}/cancel")]
    public async Task<IActionResult> CancelGoal(Guid id, [FromBody] AcknowledgeReviewBody? body, CancellationToken ct)
    {
        await mediator.Send(new CancelGoalCommand(id, body?.Comment), ct);
        return NoContent();
    }

    [HttpPost("goals/{id:guid}/reopen")]
    public async Task<IActionResult> ReopenGoal(Guid id, CancellationToken ct)
    {
        await mediator.Send(new ReopenGoalCommand(id), ct);
        return NoContent();
    }
}

public sealed record AcknowledgeReviewBody(string? Comment);
public sealed record ChangeReviewerBody(Guid? ReviewerEmployeeId);
