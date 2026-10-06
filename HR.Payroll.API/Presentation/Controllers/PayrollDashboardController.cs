namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Gateway: /payroll/dashboard — Dashboard ka pay wala hissa (payroll.view.all / payroll.approve, handler mein).</summary>
[ApiController]
[Authorize]
[Route("api/payroll/dashboard")]
public sealed class PayrollDashboardController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PayrollDashboardDto>> Get(CancellationToken ct)
        => Ok(await mediator.Send(new GetPayrollDashboardQuery(), ct));
}
