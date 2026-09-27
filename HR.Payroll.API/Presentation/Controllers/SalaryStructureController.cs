namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Salaries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Grades + templates (salary ki "recipes").</summary>
[ApiController]
[Authorize]
[Route("api/payroll")]
public sealed class SalaryStructureController(ISender mediator) : ControllerBase
{
    // ───────────── Grades ─────────────
    [HttpGet("grades")]
    public async Task<ActionResult<IReadOnlyList<SalaryGradeDto>>> GetGrades([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await mediator.Send(new GetSalaryGradesQuery(includeInactive), ct));

    [HttpPost("grades")]
    public async Task<IActionResult> CreateGrade([FromBody] SaveSalaryGradeRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateSalaryGradeCommand(request), ct);
        return Created($"api/payroll/grades/{id}", new { id });
    }

    [HttpPut("grades/{id:guid}")]
    public async Task<IActionResult> UpdateGrade(Guid id, [FromBody] SaveSalaryGradeRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateSalaryGradeCommand(id, request), ct);
        return NoContent();
    }

    // ───────────── Templates ─────────────
    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<SalaryTemplateListItemDto>>> GetTemplates([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await mediator.Send(new GetSalaryTemplatesQuery(includeInactive), ct));

    [HttpGet("templates/{id:guid}")]
    public async Task<ActionResult<SalaryTemplateDto>> GetTemplate(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetSalaryTemplateByIdQuery(id), ct));

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] SaveSalaryTemplateRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateSalaryTemplateCommand(request), ct);
        return Created($"api/payroll/templates/{id}", new { id });
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] SaveSalaryTemplateRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateSalaryTemplateCommand(id, request), ct);
        return NoContent();
    }
}
