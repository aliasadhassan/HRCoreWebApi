namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Organization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class DepartmentsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await mediator.Send(new GetDepartmentsQuery(includeInactive), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveDepartmentRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateDepartmentCommand(request), ct);
        return Created($"api/departments/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveDepartmentRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateDepartmentCommand(id, request), ct);
        return NoContent();
    }
}
