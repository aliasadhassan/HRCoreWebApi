using HR.Employee.API.Application.Employees.Commands;
using HR.Employee.API.Application.Employees.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HR.Employee.API.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EmployeesController : ControllerBase
{
    private readonly ISender _mediator; // MediatR ki lightweight interface standard separation ke liye

    // Only MediatR inject hoga controller mein
    public EmployeesController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateEmployee(
        [FromBody] CreateEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        // Request direct MediatR handler tak jaye gi aur automatic handle hogi
        var employeeId = await _mediator.Send(command, cancellationToken);

        // Standard REST API convention ke mutabiq ID return karna 200 OK ke sath
        return Ok(employeeId);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEmployeeById(Guid id, CancellationToken cancellationToken)
    {
        // Query command handle karne ke liye send karna
        var result = await _mediator.Send(new GetEmployeeByIdQuery(id), cancellationToken);

        // Agar employee nahi mila to 404 Not Found return karein
        if (result == null) return NotFound();

        // Data milne par 200 OK ke sath response bhejein
        return Ok(result);
    }
}
