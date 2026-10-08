namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Helpdesk;
using HR.Employee.API.Domain.Helpdesk;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Requests &amp; helpdesk page. Har employee apni request uthata hai; manager approval, agent (employees.edit) aur
/// assignee ki jaanch handler mein (HelpdeskAccess). Categories = settings.manage. Gateway /helpdesk/{everything}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/helpdesk")]
public sealed class HelpdeskController(ISender mediator) : ControllerBase
{
    // ---- Overview ----

    [HttpGet("summary")]
    public async Task<ActionResult<HelpdeskSummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetHelpdeskSummaryQuery(), ct));

    [HttpGet("lookups")]
    public async Task<ActionResult<HelpdeskLookupsDto>> Lookups(CancellationToken ct)
        => Ok(await mediator.Send(new GetHelpdeskLookupsQuery(), ct));

    // ---- Tickets ----

    [HttpGet("tickets")]
    public async Task<ActionResult<IReadOnlyList<TicketListItemDto>>> Tickets(
        [FromQuery] TicketScope scope = TicketScope.Mine, [FromQuery] TicketStatus? status = null, [FromQuery] bool active = false,
        [FromQuery] Guid? categoryId = null, [FromQuery] string? assignee = null, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetTicketsQuery(scope, status, active, categoryId, assignee, search), ct));

    [HttpGet("tickets/{id:guid}")]
    public async Task<ActionResult<TicketDto>> Ticket(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetTicketQuery(id), ct));

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket([FromBody] SaveTicketRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateTicketCommand(body), ct);
        return Created($"api/helpdesk/tickets/{id}", new { id });
    }

    [HttpPut("tickets/{id:guid}")]
    public async Task<IActionResult> UpdateTicket(Guid id, [FromBody] SaveTicketRequest body, CancellationToken ct)
    {
        await mediator.Send(new UpdateTicketCommand(id, body), ct);
        return NoContent();
    }

    [HttpPost("tickets/{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, [FromBody] HelpdeskNoteBody body, CancellationToken ct)
        => Run(new ApproveTicketCommand(id, body.Note), ct);

    [HttpPost("tickets/{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, [FromBody] HelpdeskNoteBody body, CancellationToken ct)
        => Run(new RejectTicketCommand(id, body.Note), ct);

    [HttpPost("tickets/{id:guid}/assign")]
    public Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketBody body, CancellationToken ct)
        => Run(new AssignTicketCommand(id, body.AssigneeEmployeeId), ct);

    [HttpPost("tickets/{id:guid}/status")]
    public Task<IActionResult> SetStatus(Guid id, [FromBody] TicketStatusBody body, CancellationToken ct)
        => Run(new SetTicketStatusCommand(id, body.Status, body.Note), ct);

    [HttpPost("tickets/{id:guid}/comments")]
    public Task<IActionResult> Comment(Guid id, [FromBody] TicketCommentBody body, CancellationToken ct)
        => Run(new CommentTicketCommand(id, body.Body, body.Internal), ct);

    [HttpPost("tickets/{id:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid id, [FromBody] TicketRatingBody body, CancellationToken ct)
        => Run(new ConfirmTicketCommand(id, body.Rating), ct);

    [HttpPut("tickets/{id:guid}/rating")]
    public Task<IActionResult> Rate(Guid id, [FromBody] TicketRatingBody body, CancellationToken ct)
        => Run(new RateTicketCommand(id, body.Rating ?? 0), ct);

    [HttpPost("tickets/{id:guid}/reopen")]
    public Task<IActionResult> Reopen(Guid id, [FromBody] HelpdeskNoteBody body, CancellationToken ct)
        => Run(new ReopenTicketCommand(id, body.Note), ct);

    [HttpPost("tickets/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, [FromBody] HelpdeskNoteBody body, CancellationToken ct)
        => Run(new CancelTicketCommand(id, body.Note), ct);

    // ---- Categories ----

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<HelpdeskCategoryDto>>> Categories(CancellationToken ct)
        => Ok(await mediator.Send(new GetHelpdeskCategoriesQuery(), ct));

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] SaveCategoryRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateCategoryCommand(body), ct);
        return Created($"api/helpdesk/categories/{id}", new { id });
    }

    [HttpPut("categories/{id:guid}")]
    public Task<IActionResult> UpdateCategory(Guid id, [FromBody] SaveCategoryRequest body, CancellationToken ct)
        => Run(new UpdateCategoryCommand(id, body), ct);

    [HttpDelete("categories/{id:guid}")]
    public Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
        => Run(new DeleteCategoryCommand(id), ct);

    [HttpPost("categories/defaults")]
    public async Task<IActionResult> AddDefaults(CancellationToken ct)
        => Ok(new { added = await mediator.Send(new AddDefaultCategoriesCommand(), ct) });

    private async Task<IActionResult> Run(IRequest command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }
}

public sealed record HelpdeskNoteBody(string? Note);
public sealed record AssignTicketBody(Guid? AssigneeEmployeeId);
public sealed record TicketStatusBody(TicketStatus Status, string? Note);
public sealed record TicketCommentBody(string Body, bool Internal);
public sealed record TicketRatingBody(byte? Rating);
