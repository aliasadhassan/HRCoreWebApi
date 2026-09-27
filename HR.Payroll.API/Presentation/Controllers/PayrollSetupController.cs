namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Setup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/payroll/setup")]
public sealed class PayrollSetupController(ISender mediator) : ControllerBase
{
    /// <summary>Payroll "on": settings + BASIC / INCOME_TAX components. Dobara chalana safe hai.</summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] InitializePayrollCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<ActionResult<PayrollSettingsDto>> GetSettings(CancellationToken ct)
        => Ok(await mediator.Send(new GetPayrollSettingsQuery(), ct));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdatePayrollSettingsCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }
}
