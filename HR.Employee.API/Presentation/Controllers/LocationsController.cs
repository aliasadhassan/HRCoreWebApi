namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Organization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class LocationsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocationDto>>> GetAll([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await mediator.Send(new GetLocationsQuery(includeInactive), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveLocationRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateLocationCommand(request), ct);
        return Created($"api/locations/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveLocationRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateLocationCommand(id, request), ct);
        return NoContent();
    }
}
