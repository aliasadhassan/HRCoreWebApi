namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Expenses;
using HR.Payroll.API.Domain.Common;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Gateway: /payroll/expenses/... — Expenses &amp; travel page.
/// me + apne claims/travel (submit/cancel): har employee. Lists: payroll.view.all / payroll.approve (handler mein).
/// Approve, reject, paid, advance dena: payroll.approve. Policy + expense types: settings.manage.
/// </summary>
[ApiController]
[Authorize]
[Route("api/payroll/expenses")]
public sealed class ExpensesController(ISender mediator) : ControllerBase
{
    // ---- Employee ----

    [HttpGet("me")]
    public async Task<ActionResult<MyExpensesDto>> Me(CancellationToken ct)
        => Ok(await mediator.Send(new GetMyExpensesQuery(), ct));

    [HttpPost("claims")]
    public async Task<IActionResult> SubmitClaim([FromBody] SubmitExpenseClaimCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/payroll/expenses/claims/{id}", new { id });
    }

    [HttpPost("claims/{id:guid}/cancel")]
    public async Task<IActionResult> CancelClaim(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelExpenseClaimCommand(id), ct);
        return NoContent();
    }

    [HttpPost("travel")]
    public async Task<IActionResult> SubmitTravel([FromBody] SubmitTravelRequestCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/payroll/expenses/travel/{id}", new { id });
    }

    [HttpPost("travel/{id:guid}/cancel")]
    public async Task<IActionResult> CancelTravel(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelTravelRequestCommand(id), ct);
        return NoContent();
    }

    // ---- Claims (HR) ----

    [HttpGet("claims")]
    public async Task<ActionResult<IReadOnlyList<ExpenseClaimDto>>> Claims(
        [FromQuery] ExpenseClaimStatus? status, [FromQuery] Guid? employeeId, CancellationToken ct)
        => Ok(await mediator.Send(new GetExpenseClaimsQuery(status, employeeId), ct));

    [HttpPost("claims/{id:guid}/approve")]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<IActionResult> ApproveClaim(Guid id, [FromBody] ApproveClaimRequest request, CancellationToken ct)
    {
        await mediator.Send(new ApproveExpenseClaimCommand(id, request.ApprovedAmount, request.Payout, request.Comment), ct);
        return NoContent();
    }

    [HttpPost("claims/{id:guid}/reject")]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<IActionResult> RejectClaim(Guid id, [FromBody] CommentRequest request, CancellationToken ct)
    {
        await mediator.Send(new RejectExpenseClaimCommand(id, request.Comment), ct);
        return NoContent();
    }

    [HttpPost("claims/{id:guid}/mark-paid")]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<IActionResult> MarkPaid(Guid id, CancellationToken ct)
    {
        await mediator.Send(new MarkExpenseClaimPaidCommand(id), ct);
        return NoContent();
    }

    // ---- Travel + advances (HR) ----

    [HttpGet("travel")]
    public async Task<ActionResult<IReadOnlyList<TravelRequestDto>>> Travel(
        [FromQuery] TravelRequestStatus? status, [FromQuery] bool advancesOnly, [FromQuery] Guid? employeeId, CancellationToken ct)
        => Ok(await mediator.Send(new GetTravelRequestsQuery(status, advancesOnly, employeeId), ct));

    [HttpPost("travel/{id:guid}/approve")]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<IActionResult> ApproveTravel(Guid id, [FromBody] ApproveTravelRequest request, CancellationToken ct)
    {
        await mediator.Send(new ApproveTravelRequestCommand(id, request.AdvanceApproved, request.Comment), ct);
        return NoContent();
    }

    [HttpPost("travel/{id:guid}/reject")]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<IActionResult> RejectTravel(Guid id, [FromBody] CommentRequest request, CancellationToken ct)
    {
        await mediator.Send(new RejectTravelRequestCommand(id, request.Comment), ct);
        return NoContent();
    }

    [HttpPost("travel/{id:guid}/pay-advance")]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<IActionResult> PayAdvance(Guid id, [FromBody] PayAdvanceRequest request, CancellationToken ct)
    {
        await mediator.Send(new PayTravelAdvanceCommand(id, request.Payout), ct);
        return NoContent();
    }

    // ---- Policy + expense types ----

    [HttpGet("policy")]
    public async Task<ActionResult<ExpensePolicyDto>> Policy(CancellationToken ct)
        => Ok(await mediator.Send(new GetExpensePolicyQuery(), ct));

    [HttpPut("policy")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> SavePolicy([FromBody] SaveExpensePolicyCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<ExpenseCategoryDto>>> Categories(CancellationToken ct)
        => Ok(await mediator.Send(new GetExpenseCategoriesQuery(), ct));

    [HttpPost("categories")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(request.ToCommand(null), ct);
        return Created($"api/payroll/expenses/categories/{id}", new { id });
    }

    [HttpPut("categories/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CategoryRequest request, CancellationToken ct)
    {
        await mediator.Send(request.ToCommand(id), ct);
        return NoContent();
    }

    public sealed record ApproveClaimRequest(decimal? ApprovedAmount, PayoutMethod? Payout, string? Comment);
    public sealed record ApproveTravelRequest(decimal? AdvanceApproved, string? Comment);
    public sealed record PayAdvanceRequest(PayoutMethod Payout);
    public sealed record CommentRequest(string? Comment);

    public sealed record CategoryRequest(string Code, string Name, string? Description, decimal? MaxPerClaim, bool ReceiptRequired, bool IsActive, short SortOrder)
    {
        public SaveExpenseCategoryCommand ToCommand(Guid? id) => new(id, Code, Name, Description, MaxPerClaim, ReceiptRequired, IsActive, SortOrder);
    }
}
