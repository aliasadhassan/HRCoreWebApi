namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Lifecycle;
using HR.Employee.API.Domain.Lifecycle;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Onboarding & exit page: cases + tasks (HR), my tasks (har koi), checklist templates.
/// Task complete/reopen ka access handler mein (assignee bhi kar sakta hai), baaki HR permissions.
/// </summary>
[ApiController]
[Authorize]
[Route("api/lifecycle")]
public sealed class LifecycleController(ISender mediator) : ControllerBase
{
    // ---- Overview ----

    [HttpGet("summary")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<LifecycleSummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetLifecycleSummaryQuery(), ct));

    [HttpGet("cases")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<IReadOnlyList<LifecycleCaseListItemDto>>> Cases(
        [FromQuery] LifecycleKind? kind, [FromQuery] CaseStatus? status, [FromQuery] string? search, CancellationToken ct)
        => Ok(await mediator.Send(new GetLifecycleCasesQuery(kind, status, search), ct));

    [HttpGet("cases/{id:guid}")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<LifecycleCaseDto>> Case(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetLifecycleCaseQuery(id), ct));

    [HttpGet("candidates")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<ActionResult<IReadOnlyList<LifecycleCandidateDto>>> Candidates(
        [FromQuery] LifecycleKind kind, [FromQuery] string? search, CancellationToken ct)
        => Ok(await mediator.Send(new GetLifecycleCandidatesQuery(kind, search), ct));

    [HttpGet("my-tasks")]
    public async Task<ActionResult<IReadOnlyList<MyLifecycleTaskDto>>> MyTasks(CancellationToken ct)
        => Ok(await mediator.Send(new GetMyLifecycleTasksQuery(), ct));

    // ---- Start / close ----

    [HttpPost("onboarding")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> StartOnboarding([FromBody] StartOnboardingCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/lifecycle/cases/{id}", new { id });
    }

    [HttpPost("exit")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> StartExit([FromBody] StartExitCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/lifecycle/cases/{id}", new { id });
    }

    [HttpPut("cases/{id:guid}/exit-details")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> UpdateExitDetails(Guid id, [FromBody] UpdateExitDetailsRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateExitDetailsCommand(id, request), ct);
        return NoContent();
    }

    [HttpPut("cases/{id:guid}/notes")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> UpdateNotes(Guid id, [FromBody] NotesRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateCaseNotesCommand(id, request.Notes), ct);
        return NoContent();
    }

    [HttpPost("cases/{id:guid}/complete")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CompleteLifecycleCaseCommand(id), ct);
        return NoContent();
    }

    [HttpPost("cases/{id:guid}/cancel")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelLifecycleCaseCommand(id), ct);
        return NoContent();
    }

    // ---- Tasks ----

    [HttpPost("cases/{id:guid}/tasks")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> AddTask(Guid id, [FromBody] SaveLifecycleTaskRequest request, CancellationToken ct)
    {
        var taskId = await mediator.Send(new AddLifecycleTaskCommand(id, request), ct);
        return Created($"api/lifecycle/cases/{id}", new { id = taskId });
    }

    [HttpPut("cases/{id:guid}/tasks/{taskId:guid}")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> UpdateTask(Guid id, Guid taskId, [FromBody] SaveLifecycleTaskRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateLifecycleTaskCommand(id, taskId, request), ct);
        return NoContent();
    }

    [HttpDelete("cases/{id:guid}/tasks/{taskId:guid}")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> RemoveTask(Guid id, Guid taskId, CancellationToken ct)
    {
        await mediator.Send(new RemoveLifecycleTaskCommand(id, taskId), ct);
        return NoContent();
    }

    [HttpPost("cases/{id:guid}/tasks/{taskId:guid}/complete")]
    public Task<IActionResult> CompleteTask(Guid id, Guid taskId, [FromBody] NoteRequest? request, CancellationToken ct)
        => ChangeTask(id, taskId, TaskAction.Complete, request?.Note, ct);

    [HttpPost("cases/{id:guid}/tasks/{taskId:guid}/skip")]
    [HasPermission(Permissions.EmployeesEdit)]
    public Task<IActionResult> SkipTask(Guid id, Guid taskId, [FromBody] NoteRequest? request, CancellationToken ct)
        => ChangeTask(id, taskId, TaskAction.Skip, request?.Note, ct);

    [HttpPost("cases/{id:guid}/tasks/{taskId:guid}/reopen")]
    public Task<IActionResult> ReopenTask(Guid id, Guid taskId, CancellationToken ct)
        => ChangeTask(id, taskId, TaskAction.Reopen, null, ct);

    private async Task<IActionResult> ChangeTask(Guid id, Guid taskId, TaskAction action, string? note, CancellationToken ct)
    {
        await mediator.Send(new ChangeLifecycleTaskCommand(id, taskId, action, note), ct);
        return NoContent();
    }

    // ---- Templates ----

    [HttpGet("templates")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<IReadOnlyList<ChecklistTemplateDto>>> Templates(
        [FromQuery] LifecycleKind? kind, [FromQuery] bool includeInactive = true, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetChecklistTemplatesQuery(kind, includeInactive), ct));

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] SaveChecklistTemplateRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateChecklistTemplateCommand(request), ct);
        return Created($"api/lifecycle/templates/{id}", new { id });
    }

    [HttpPost("templates/starter")]
    public async Task<ActionResult<object>> CreateStarterTemplates(CancellationToken ct)
        => Ok(new { created = await mediator.Send(new CreateStarterTemplatesCommand(), ct) });

    [HttpPut("templates/{id:guid}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] SaveChecklistTemplateRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateChecklistTemplateCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("templates/{id:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteChecklistTemplateCommand(id), ct);
        return NoContent();
    }

    public sealed record NotesRequest(string? Notes);
    public sealed record NoteRequest(string? Note);
}
