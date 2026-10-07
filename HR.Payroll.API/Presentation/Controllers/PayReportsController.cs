namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Gateway: /payroll/reports/pay, /payroll/reports/register. Permission handler mein (view.all ya approve).</summary>
[ApiController]
[Authorize]
[Route("api/payroll/reports")]
public sealed class PayReportsController(ISender mediator) : ControllerBase
{
    [HttpGet("pay")]
    public async Task<ActionResult<PayReportDto>> Pay([FromQuery] int year, CancellationToken ct)
        => Ok(await mediator.Send(new GetPayReportQuery(year), ct));

    [HttpGet("register")]
    public async Task<ActionResult<PayRegisterDto>> Register([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => Ok(await mediator.Send(new GetPayRegisterQuery(year, month), ct));
}
