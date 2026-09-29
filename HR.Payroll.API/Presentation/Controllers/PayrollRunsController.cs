namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Runs;
using HR.Payroll.API.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public sealed record CreatePayrollRunRequest(Guid PayGroupId, Guid PayPeriodId, RunType? RunType);

/// <summary>Draft → calculate (jitni baar chahe) → approve. Approve ke baad kuch nahi badalta.</summary>
[ApiController]
[Authorize]
[Route("api/payroll")]
public sealed class PayrollRunsController(ISender mediator) : ControllerBase
{
    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<PayrollRunDto>>> GetRuns([FromQuery] Guid? payGroupId, CancellationToken ct)
        => Ok(await mediator.Send(new GetPayrollRunsQuery(payGroupId), ct));

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<PayrollRunDto>> GetRun(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetPayrollRunByIdQuery(id), ct));

    [HttpPost("runs")]
    public async Task<IActionResult> CreateRun([FromBody] CreatePayrollRunRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreatePayrollRunCommand(request.PayGroupId, request.PayPeriodId, request.RunType ?? RunType.Regular), ct);
        return Created($"api/payroll/runs/{id}", new { id });
    }

    [HttpPost("runs/{id:guid}/calculate")]
    public async Task<ActionResult<PayrollRunDto>> Calculate(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new CalculatePayrollRunCommand(id), ct));

    [HttpPost("runs/{id:guid}/approve")]
    public async Task<ActionResult<PayrollRunDto>> Approve(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new ApprovePayrollRunCommand(id), ct));

    [HttpPost("runs/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelPayrollRunCommand(id), ct);
        return NoContent();
    }

    [HttpGet("runs/{id:guid}/payslips")]
    public async Task<ActionResult<IReadOnlyList<PayslipListItemDto>>> GetPayslips(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetRunPayslipsQuery(id), ct));

    [HttpGet("payslips/{id:guid}")]
    public async Task<ActionResult<PayslipDto>> GetPayslip(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetPayslipByIdQuery(id), ct));
}
