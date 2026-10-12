namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Engagement;
using HR.Employee.API.Domain.Engagement;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Engagement page: surveys / pulse aur recognition. Har employee apne surveys ka jawab deta hai aur recognition de sakta hai;
/// employees.edit surveys chalata hai, employees.view results dekhta hai (EngagementAccess). Gateway /engagement/{everything}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/engagement")]
public sealed class EngagementController(ISender mediator) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<EngagementSummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetEngagementSummaryQuery(), ct));

    [HttpGet("lookups")]
    public async Task<ActionResult<EngagementLookupsDto>> Lookups(CancellationToken ct)
        => Ok(await mediator.Send(new GetEngagementLookupsQuery(), ct));

    // ---- Surveys ----

    [HttpGet("surveys")]
    public async Task<ActionResult<IReadOnlyList<SurveyListItemDto>>> Surveys(
        [FromQuery] string? scope = null, [FromQuery] SurveyStatus? status = null, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetSurveysQuery(scope, status, search), ct));

    [HttpGet("surveys/templates")]
    public async Task<ActionResult<IReadOnlyList<SurveyTemplateDto>>> Templates(CancellationToken ct)
        => Ok(await mediator.Send(new GetSurveyTemplatesQuery(), ct));

    [HttpPost("surveys/templates/{key}")]
    public async Task<IActionResult> FromTemplate(string key, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateSurveyFromTemplateCommand(key), ct);
        return Created($"api/engagement/surveys/{id}", new { id });
    }

    [HttpGet("surveys/{id:guid}")]
    public async Task<ActionResult<SurveyDetailDto>> Survey(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetSurveyQuery(id), ct));

    [HttpGet("surveys/{id:guid}/results")]
    public async Task<ActionResult<SurveyResultsDto>> Results(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetSurveyResultsQuery(id), ct));

    [HttpPost("surveys")]
    public async Task<IActionResult> CreateSurvey([FromBody] SaveSurveyRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateSurveyCommand(body), ct);
        return Created($"api/engagement/surveys/{id}", new { id });
    }

    [HttpPut("surveys/{id:guid}")]
    public Task<IActionResult> UpdateSurvey(Guid id, [FromBody] SaveSurveyRequest body, CancellationToken ct)
        => Run(new UpdateSurveyCommand(id, body), ct);

    [HttpDelete("surveys/{id:guid}")]
    public Task<IActionResult> DeleteSurvey(Guid id, CancellationToken ct)
        => Run(new DeleteSurveyCommand(id), ct);

    [HttpPost("surveys/{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id, CancellationToken ct)
        => Run(new PublishSurveyCommand(id), ct);

    [HttpPost("surveys/{id:guid}/close")]
    public Task<IActionResult> Close(Guid id, CancellationToken ct)
        => Run(new CloseSurveyCommand(id), ct);

    [HttpPost("surveys/{id:guid}/reopen")]
    public Task<IActionResult> Reopen(Guid id, [FromBody] ReopenBody body, CancellationToken ct)
        => Run(new ReopenSurveyCommand(id, body.ClosesOn), ct);

    [HttpPost("surveys/{id:guid}/responses")]
    public Task<IActionResult> Submit(Guid id, [FromBody] SubmitSurveyBody body, CancellationToken ct)
        => Run(new SubmitSurveyCommand(id, body.Answers), ct);

    // ---- Recognition ----

    [HttpGet("recognitions")]
    public async Task<ActionResult<IReadOnlyList<RecognitionDto>>> Recognitions(
        [FromQuery] string? scope = null, [FromQuery] Guid? employeeId = null, [FromQuery] RecognitionValue? value = null,
        [FromQuery] Guid? departmentId = null, [FromQuery] DateTime? before = null, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetRecognitionsQuery(scope, employeeId, value, departmentId, before, take), ct));

    [HttpGet("recognitions/stats")]
    public async Task<ActionResult<RecognitionStatsDto>> Stats([FromQuery] string? period = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetRecognitionStatsQuery(period), ct));

    [HttpPost("recognitions")]
    public async Task<IActionResult> Give([FromBody] GiveRecognitionRequest body, CancellationToken ct)
        => Ok(new { count = await mediator.Send(new GiveRecognitionCommand(body), ct) });

    [HttpDelete("recognitions/{id:guid}")]
    public Task<IActionResult> DeleteRecognition(Guid id, CancellationToken ct)
        => Run(new DeleteRecognitionCommand(id), ct);

    [HttpPost("recognitions/{id:guid}/cheer")]
    public async Task<IActionResult> Cheer(Guid id, CancellationToken ct)
        => Ok(new { cheers = await mediator.Send(new CheerRecognitionCommand(id, true), ct) });

    [HttpDelete("recognitions/{id:guid}/cheer")]
    public async Task<IActionResult> Uncheer(Guid id, CancellationToken ct)
        => Ok(new { cheers = await mediator.Send(new CheerRecognitionCommand(id, false), ct) });

    private async Task<IActionResult> Run(IRequest command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }
}

public sealed record ReopenBody(DateOnly? ClosesOn);
public sealed record SubmitSurveyBody(IReadOnlyList<SurveyAnswerRequest> Answers);
