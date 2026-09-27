namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Tax;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/payroll/tax")]
public sealed class TaxRulesController(ISender mediator) : ControllerBase
{
    [HttpGet("regimes")]
    public async Task<ActionResult<IReadOnlyList<TaxRegimeDto>>> GetRegimes([FromQuery] string? countryCode, CancellationToken ct)
        => Ok(await mediator.Send(new GetTaxRegimesQuery(countryCode), ct));

    [HttpPost("regimes")]
    public async Task<IActionResult> CreateRegime([FromBody] CreateTaxRegimeCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/payroll/tax/regimes/{id}", new { id });
    }

    [HttpGet("contributions")]
    public async Task<ActionResult<IReadOnlyList<ContributionRuleDto>>> GetContributions([FromQuery] string? countryCode, CancellationToken ct)
        => Ok(await mediator.Send(new GetContributionRulesQuery(countryCode), ct));

    [HttpPost("contributions")]
    public async Task<IActionResult> CreateContribution([FromBody] CreateContributionRuleCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/payroll/tax/contributions/{id}", new { id });
    }
}
