namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Organization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class DesignationsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DesignationDto>>> GetAll([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await mediator.Send(new GetDesignationsQuery(includeInactive), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveDesignationRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateDesignationCommand(request), ct);
        return Created($"api/designations/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveDesignationRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateDesignationCommand(id, request), ct);
        return NoContent();
    }
}
