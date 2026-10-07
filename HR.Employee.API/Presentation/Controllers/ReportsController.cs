namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Reports;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Gateway: /reports/people, /reports/time. Poori company ka data, isliye employees.view.</summary>
[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(ISender mediator) : ControllerBase
{
    [HttpGet("people")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<PeopleReportDto>> People([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? departmentId, CancellationToken ct)
        => Ok(await mediator.Send(new GetPeopleReportQuery(from, to, departmentId), ct));

    [HttpGet("time")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<TimeReportDto>> Time([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? departmentId, CancellationToken ct)
        => Ok(await mediator.Send(new GetTimeReportQuery(from, to, departmentId), ct));
}
