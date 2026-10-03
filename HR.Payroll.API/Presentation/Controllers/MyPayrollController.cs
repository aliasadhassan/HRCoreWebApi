namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Runs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Gateway: /payroll/me/... — har employee apni payslips dekh sake (payroll.view.own).</summary>
[ApiController]
[Authorize]
[Route("api/payroll/me")]
public sealed class MyPayrollController(ISender mediator) : ControllerBase
{
    [HttpGet("payslips")]
    public async Task<ActionResult<IReadOnlyList<MyPayslipDto>>> Payslips(CancellationToken ct)
        => Ok(await mediator.Send(new GetMyPayslipsQuery(), ct));

    [HttpGet("payslips/{id:guid}")]
    public async Task<ActionResult<PayslipDto>> Payslip(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetMyPayslipQuery(id), ct));
}
