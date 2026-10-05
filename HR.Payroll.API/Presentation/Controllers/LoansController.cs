namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Loans;
using HR.Payroll.API.Domain.Common;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Gateway: /payroll/loans/... — Loans &amp; advances page.
/// me + requests (submit/cancel): har employee. Lists: payroll.view.all / payroll.approve (handler mein).
/// Approve, reject, naya loan, pause/resume/cancel, installment: payroll.approve. Policy badalna: settings.manage.
/// </summary>
[ApiController]
[Authorize]
[Route("api/payroll/loans")]
public sealed class LoansController(ISender mediator) : ControllerBase
{
    // ---- Employee ----

    [HttpGet("me")]
    public async Task<ActionResult<MyLoansDto>> Me(CancellationToken ct)
        => Ok(await mediator.Send(new GetMyLoansQuery(), ct));

    [HttpPost("requests")]
    public async Task<IActionResult> Submit([FromBody] SubmitLoanRequestCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/payroll/loans/requests/{id}", new { id });
    }

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelLoanRequestCommand(id), ct);
        return NoContent();
    }

    // ---- Requests (HR) ----

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<LoanRequestDto>>> Requests(
        [FromQuery] LoanRequestStatus? status, [FromQuery] Guid? employeeId, CancellationToken ct)
        => Ok(await mediator.Send(new GetLoanRequestsQuery(status, employeeId), ct));

    [HttpPost("requests/{id:guid}/approve")]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRequest request, CancellationToken ct)
    {
        var loanId = await mediator.Send(new ApproveLoanRequestCommand(
            id, request.Amount, request.InstallmentAmount, request.StartDate, request.DeductionComponentId, request.Comment), ct);
        return Ok(new { loanId });
    }

    [HttpPost("requests/{id:guid}/reject")]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] CommentRequest request, CancellationToken ct)
    {
        await mediator.Send(new RejectLoanRequestCommand(id, request.Comment), ct);
        return NoContent();
    }

    // ---- Loans (HR) ----

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LoanDto>>> Loans(
        [FromQuery] LoanStatus? status, [FromQuery] LoanType? loanType, [FromQuery] Guid? employeeId, CancellationToken ct)
        => Ok(await mediator.Send(new GetLoansQuery(status, loanType, employeeId), ct));

    [HttpPost]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<IActionResult> Create([FromBody] CreateLoanCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/payroll/loans/{id}", new { id });
    }

    [HttpPut("{id:guid}/installment")]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<IActionResult> ChangeInstallment(Guid id, [FromBody] InstallmentRequest request, CancellationToken ct)
    {
        await mediator.Send(new ChangeLoanInstallmentCommand(id, request.InstallmentAmount), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/{op:regex(^(pause|resume|cancel)$)}")]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<IActionResult> ChangeStatus(Guid id, string op, CancellationToken ct)
    {
        await mediator.Send(new ChangeLoanStatusCommand(id, Enum.Parse<LoanAction>(op, ignoreCase: true)), ct);
        return NoContent();
    }

    // ---- Repayments ----

    [HttpGet("repayments")]
    public async Task<ActionResult<IReadOnlyList<LoanRepaymentDto>>> Repayments(
        [FromQuery] Guid? loanId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await mediator.Send(new GetLoanRepaymentsQuery(loanId, from, to), ct));

    // ---- Policy ----

    [HttpGet("policy")]
    public async Task<ActionResult<LoanPolicyDto>> Policy(CancellationToken ct)
        => Ok(await mediator.Send(new GetLoanPolicyQuery(), ct));

    [HttpPut("policy")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> SavePolicy([FromBody] SaveLoanPolicyCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }

    public sealed record ApproveRequest(decimal? Amount, decimal? InstallmentAmount, DateOnly? StartDate, Guid? DeductionComponentId, string? Comment);
    public sealed record CommentRequest(string? Comment);
    public sealed record InstallmentRequest(decimal InstallmentAmount);
}
