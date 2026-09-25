namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Common.Models;
using HR.Employee.API.Application.Employees.Commands;
using HR.Employee.API.Application.Employees.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Patla controller: sirf HTTP ↔ MediatR. Koi DbContext, cache ya business logic yahan nahi.</summary>
[ApiController]
[Authorize]   // TODO (RBAC phase): [Authorize(Policy = "employees.view")] waghera
[Route("api/[controller]")]
public sealed class EmployeesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<EmployeeListItemDto>>> GetEmployees([FromQuery] GetEmployeesQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDetailsDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetEmployeeByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}/profile")]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] UpdateEmployeeProfileCommand command, CancellationToken ct)
    {
        await mediator.Send(command with { EmployeeId = id }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/job-change")]
    public async Task<IActionResult> ChangeJob(Guid id, [FromBody] ChangeEmployeeJobCommand command, CancellationToken ct)
    {
        await mediator.Send(command with { EmployeeId = id }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmProbation(Guid id, [FromBody] ConfirmEmployeeCommand command, CancellationToken ct)
    {
        await mediator.Send(command with { EmployeeId = id }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/exit")]
    public async Task<IActionResult> Exit(Guid id, [FromBody] ExitEmployeeCommand command, CancellationToken ct)
    {
        await mediator.Send(command with { EmployeeId = id }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteEmployeeCommand(id), ct);
        return NoContent();
    }
}
