namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Setup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/payroll/components")]
public sealed class PayComponentsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PayComponentDto>>> GetAll([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await mediator.Send(new GetPayComponentsQuery(includeInactive), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePayComponentRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreatePayComponentCommand(request), ct);
        return Created($"api/payroll/components/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SavePayComponentRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdatePayComponentCommand(id, request), ct);
        return NoContent();
    }
}
