namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Ai;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public sealed record AskBody(Guid? ConversationId, string Question, string? Language);
public sealed record ScreenBody(string? CvText, string? CvPdfBase64);
public sealed record InsightStatusBody([property: JsonRequired] bool Dismissed);

/// <summary>
/// AI assistant: chat, insights, CV screening, settings. Ijazatein AiAccess (ai.use / ai.insights / ai.screening / ai.manage).
/// API key na ho to home "Configured = false" deta hai aur koi call model tak nahi jati. Gateway /ai/{everything}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/ai")]
public sealed class AiController(ISender mediator) : ControllerBase
{
    [HttpGet("home")]
    public async Task<ActionResult<AiHomeDto>> Home(CancellationToken ct)
        => Ok(await mediator.Send(new GetAiHomeQuery(), ct));

    [HttpPut("settings")]
    public async Task<ActionResult<AiSettingsDto>> Settings([FromBody] AiSettingsRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateAiSettingsCommand(body), ct));

    // ---- Chat ----

    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<ConversationItemDto>>> Conversations(CancellationToken ct)
        => Ok(await mediator.Send(new GetConversationsQuery(), ct));

    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<ConversationDto>> Conversation(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetConversationQuery(id), ct));

    [HttpPost("ask")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<ActionResult<AskResultDto>> Ask([FromBody] AskBody body, CancellationToken ct)
        => Ok(await mediator.Send(new AskCommand(body.ConversationId, body.Question, body.Language), ct));

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteConversationCommand(id), ct);
        return NoContent();
    }

    // ---- Insights ----

    [HttpGet("insights")]
    public async Task<ActionResult<InsightsDto>> Insights([FromQuery] bool dismissed, CancellationToken ct)
        => Ok(await mediator.Send(new GetInsightsQuery(dismissed), ct));

    [HttpPost("insights/run")]
    public async Task<ActionResult<InsightsDto>> RunInsights(CancellationToken ct)
        => Ok(await mediator.Send(new RunInsightsCommand(), ct));

    [HttpPost("insights/{id:guid}/status")]
    public async Task<IActionResult> InsightStatus(Guid id, [FromBody] InsightStatusBody body, CancellationToken ct)
    {
        await mediator.Send(new SetInsightStatusCommand(id, body.Dismissed), ct);
        return NoContent();
    }

    // ---- Screening ----

    [HttpGet("screening")]
    public async Task<ActionResult<ScreeningBoardDto>> Screening([FromQuery] Guid? jobId, CancellationToken ct)
        => Ok(await mediator.Send(new GetScreeningBoardQuery(jobId), ct));

    [HttpGet("screening/{applicationId:guid}")]
    public async Task<ActionResult<ScreeningDto?>> ScreeningDetail(Guid applicationId, CancellationToken ct)
        => Ok(await mediator.Send(new GetScreeningQuery(applicationId), ct));

    [HttpPost("screening/{applicationId:guid}")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<ScreeningDto>> Screen(Guid applicationId, [FromBody] ScreenBody body, CancellationToken ct)
        => Ok(await mediator.Send(new ScreenApplicationCommand(applicationId, body.CvText, body.CvPdfBase64), ct));
}
