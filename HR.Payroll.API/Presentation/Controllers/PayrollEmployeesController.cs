namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Common.Models;
using HR.Payroll.API.Application.Employees;
using HR.Payroll.API.Application.Salaries;
using HR.Payroll.API.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public sealed record SalaryOverrideRequest(CalcType CalcType, decimal? Amount, decimal? Percentage, Guid? BaseComponentId);

[ApiController]
[Authorize]
[Route("api/payroll/employees")]
public sealed class PayrollEmployeesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PayrollEmployeeDto>>> GetAll([FromQuery] GetPayrollEmployeesQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpPost("pay-group")]
    public async Task<IActionResult> AssignPayGroup([FromBody] AssignPayGroupCommand command, CancellationToken ct)
        => Ok(new { updated = await mediator.Send(command, ct) });

    // ───────────── Salary ─────────────
    [HttpGet("{employeeId:guid}/salaries")]
    public async Task<ActionResult<IReadOnlyList<EmployeeSalaryDto>>> GetSalaryHistory(Guid employeeId, CancellationToken ct)
        => Ok(await mediator.Send(new GetSalaryHistoryQuery(employeeId), ct));

    /// <summary>Pehli salary ya increment. Band se bahar ho to "warning" field mein message.</summary>
    [HttpPost("{employeeId:guid}/salaries")]
    public async Task<ActionResult<AssignSalaryResult>> AssignSalary(Guid employeeId, [FromBody] AssignSalaryCommand command, CancellationToken ct)
        => Ok(await mediator.Send(command with { EmployeeId = employeeId }, ct));

    [HttpPut("{employeeId:guid}/salary/components/{componentId:guid}")]
    public async Task<IActionResult> SetOverride(Guid employeeId, Guid componentId, [FromBody] SalaryOverrideRequest request, CancellationToken ct)
    {
        await mediator.Send(new SetSalaryOverrideCommand(employeeId, componentId, request.CalcType, request.Amount, request.Percentage, request.BaseComponentId), ct);
        return NoContent();
    }

    [HttpPost("{employeeId:guid}/salary/components/{componentId:guid}/exclude")]
    public async Task<IActionResult> ExcludeComponent(Guid employeeId, Guid componentId, CancellationToken ct)
    {
        await mediator.Send(new ExcludeSalaryComponentCommand(employeeId, componentId), ct);
        return NoContent();
    }

    [HttpDelete("{employeeId:guid}/salary/components/{componentId:guid}")]
    public async Task<IActionResult> RemoveOverride(Guid employeeId, Guid componentId, CancellationToken ct)
    {
        await mediator.Send(new RemoveSalaryOverrideCommand(employeeId, componentId), ct);
        return NoContent();
    }
}
