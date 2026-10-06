namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Gateway: /dashboard/summary. Har logged-in user; scope handler mein (company ya apna department).</summary>
[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(ISender mediator) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<PeopleDashboardDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetPeopleDashboardQuery(), ct));
}
