namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Setup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/payroll/pay-groups")]
public sealed class PayGroupsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PayGroupDto>>> GetAll([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await mediator.Send(new GetPayGroupsQuery(includeInactive), ct));

    /// <summary>Banate hi pehle 3 periods bhi ban jate hain.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePayGroupRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreatePayGroupCommand(request), ct);
        return Created($"api/payroll/pay-groups/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePayGroupRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdatePayGroupCommand(id, request), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/periods")]
    public async Task<ActionResult<IReadOnlyList<PayPeriodDto>>> GetPeriods(Guid id, [FromQuery] short? year, CancellationToken ct)
        => Ok(await mediator.Send(new GetPayPeriodsQuery(id, year), ct));

    [HttpPost("{id:guid}/periods/generate")]
    public async Task<IActionResult> GeneratePeriods(Guid id, [FromQuery] int count = 3, CancellationToken ct = default)
        => Ok(new { generated = await mediator.Send(new GeneratePayPeriodsCommand(id, count), ct) });
}
