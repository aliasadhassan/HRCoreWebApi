namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.ReportBuilder;
using HR.Shared.Library.Reporting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public sealed record RunReportBody(string Source, ReportSpec Spec, DateOnly? From, DateOnly? To);
public sealed record TemplateCreateBody(TemplateSettingsRequest Settings, string? HtmlEn, string? HtmlAr);
public sealed record TemplateSettingsBody(TemplateSettingsRequest Settings, uint? RowVersion);
public sealed record TemplateDraftBody(string? HtmlEn, string? HtmlAr, uint? RowVersion);
public sealed record RowVersionBody(uint? RowVersion);
public sealed record LetterDataBody(IReadOnlyList<Guid> EmployeeIds);
public sealed record VoidLetterBody(string? Reason);

/// <summary>
/// Report builder: apne table reports (Employee API ke sources; payroll sources Payroll API par) aur letter / certificate
/// templates (TipTap HTML) se letters jari karna. Ijazatein ReportBuilderAccess mein. Gateway /report-builder/{everything}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/report-builder")]
public sealed class ReportBuilderController(ISender mediator) : ControllerBase
{
    [HttpGet("home")]
    public async Task<ActionResult<ReportBuilderHomeDto>> Home(CancellationToken ct)
        => Ok(await mediator.Send(new GetReportBuilderHomeQuery(), ct));

    [HttpPost("run")]
    public async Task<ActionResult<RunResultDto>> Run([FromBody] RunReportBody body, CancellationToken ct)
        => Ok(await mediator.Send(new RunReportQuery(body.Source, body.Spec ?? new ReportSpec(), body.From, body.To), ct));

    // ---- Saved reports ----

    [HttpGet("reports/{id:guid}")]
    public async Task<ActionResult<SavedReportDto>> Report(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetSavedReportQuery(id), ct));

    [HttpPost("reports")]
    public async Task<IActionResult> CreateReport([FromBody] ReportRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateReportCommand(body), ct);
        return Created($"api/report-builder/reports/{id}", new { id });
    }

    [HttpPut("reports/{id:guid}")]
    public Task<IActionResult> UpdateReport(Guid id, [FromBody] ReportRequest body, CancellationToken ct)
        => Run(new UpdateReportCommand(id, body), ct);

    [HttpDelete("reports/{id:guid}")]
    public Task<IActionResult> DeleteReport(Guid id, CancellationToken ct)
        => Run(new DeleteReportCommand(id), ct);

    [HttpPost("reports/{id:guid}/ran")]
    public Task<IActionResult> MarkRun(Guid id, CancellationToken ct)
        => Run(new MarkReportRunCommand(id), ct);

    // ---- Templates ----

    [HttpGet("templates/{id:guid}")]
    public async Task<ActionResult<TemplateDetailDto>> Template(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetTemplateQuery(id), ct));

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] TemplateCreateBody body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateTemplateCommand(body.Settings, body.HtmlEn, body.HtmlAr), ct);
        return Created($"api/report-builder/templates/{id}", new { id });
    }

    [HttpPut("templates/{id:guid}/settings")]
    public async Task<ActionResult<SaveResultDto>> TemplateSettings(Guid id, [FromBody] TemplateSettingsBody body, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateTemplateSettingsCommand(id, body.Settings, body.RowVersion), ct));

    [HttpPut("templates/{id:guid}/draft")]
    public async Task<ActionResult<SaveResultDto>> TemplateDraft(Guid id, [FromBody] TemplateDraftBody body, CancellationToken ct)
        => Ok(await mediator.Send(new SaveTemplateDraftCommand(id, body.HtmlEn, body.HtmlAr, body.RowVersion), ct));

    [HttpPost("templates/{id:guid}/publish")]
    public async Task<ActionResult<SaveResultDto>> Publish(Guid id, [FromBody] RowVersionBody body, CancellationToken ct)
        => Ok(await mediator.Send(new PublishTemplateCommand(id, body.RowVersion), ct));

    [HttpPost("templates/{id:guid}/archive")]
    public Task<IActionResult> Archive(Guid id, CancellationToken ct) => Run(new ArchiveTemplateCommand(id, true), ct);

    [HttpPost("templates/{id:guid}/restore")]
    public Task<IActionResult> Restore(Guid id, CancellationToken ct) => Run(new ArchiveTemplateCommand(id, false), ct);

    [HttpDelete("templates/{id:guid}")]
    public Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct) => Run(new DeleteTemplateCommand(id), ct);

    [HttpPost("templates/{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken ct)
    {
        var copy = await mediator.Send(new DuplicateTemplateCommand(id), ct);
        return Created($"api/report-builder/templates/{copy}", new { id = copy });
    }

    // ---- Letters ----

    [HttpGet("people")]
    public async Task<ActionResult<IReadOnlyList<LetterPersonDto>>> People(
        [FromQuery] string? search = null, [FromQuery] Guid? departmentId = null, [FromQuery] bool includeExited = false, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetLetterPeopleQuery(search, departmentId, includeExited), ct));

    [HttpPost("letter-data")]
    public async Task<ActionResult<IReadOnlyList<LetterDataDto>>> LetterData([FromBody] LetterDataBody body, CancellationToken ct)
        => Ok(await mediator.Send(new GetLetterDataQuery(body.EmployeeIds ?? []), ct));

    [HttpGet("letters")]
    public async Task<ActionResult<IReadOnlyList<IssuedLetterListItemDto>>> Letters(
        [FromQuery] Guid? employeeId = null, [FromQuery] Guid? templateId = null, [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null, [FromQuery] string? search = null, [FromQuery] bool? isVoid = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetLettersQuery(employeeId, templateId, from, to, search, isVoid), ct));

    [HttpGet("letters/{id:guid}")]
    public async Task<ActionResult<IssuedLetterDto>> Letter(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetLetterQuery(id), ct));

    [HttpPost("letters")]
    public async Task<ActionResult<IReadOnlyList<IssuedLetterResultDto>>> Issue([FromBody] IssueLettersRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new IssueLettersCommand(body), ct));

    [HttpPost("letters/{id:guid}/void")]
    public Task<IActionResult> Void(Guid id, [FromBody] VoidLetterBody body, CancellationToken ct)
        => Run(new VoidLetterCommand(id, body.Reason), ct);

    private async Task<IActionResult> Run(IRequest command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }
}
