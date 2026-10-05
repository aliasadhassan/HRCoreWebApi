namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Leaves;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Leaves page (my leave, approvals, all requests) + Leave setup page (types, holidays, policies, approvals).
/// Requests: kaun kya dekh/approve kar sakta hai handler mein. Setup: dekhna koi bhi, badalna settings.manage.
/// </summary>
[ApiController]
[Authorize]
[Route("api/leave")]
public sealed class LeaveController(ISender mediator) : ControllerBase
{
    // ---- Me ----

    [HttpGet("me")]
    public async Task<ActionResult<MyLeaveDto>> Me([FromQuery] short? year, CancellationToken ct)
        => Ok(await mediator.Send(new GetMyLeaveQuery(year), ct));

    [HttpGet("me/preview")]
    public async Task<ActionResult<LeavePreviewDto>> Preview(
        [FromQuery] DateOnly start, [FromQuery] DateOnly end, [FromQuery] bool halfDay, CancellationToken ct)
        => Ok(await mediator.Send(new PreviewLeaveQuery(start, end, halfDay), ct));

    // ---- Requests ----

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> Requests([FromQuery] GetLeaveRequestsQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpPost("requests")]
    public async Task<IActionResult> Submit([FromBody] SubmitLeaveRequestCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/leave/requests/{id}", new { id });
    }

    [HttpPost("requests/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] CommentRequest request, CancellationToken ct)
    {
        await mediator.Send(new ApproveLeaveRequestCommand(id, request.Comment), ct);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] CommentRequest request, CancellationToken ct)
    {
        await mediator.Send(new RejectLeaveRequestCommand(id, request.Comment), ct);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelLeaveRequestCommand(id), ct);
        return NoContent();
    }

    // ---- Setup: leave types ----

    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<LeaveTypeDto>>> Types([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await mediator.Send(new GetLeaveTypesQuery(includeInactive), ct));

    [HttpPost("types")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> CreateType([FromBody] SaveLeaveTypeRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateLeaveTypeCommand(request), ct);
        return Created($"api/leave/types/{id}", new { id });
    }

    [HttpPut("types/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> UpdateType(Guid id, [FromBody] SaveLeaveTypeRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateLeaveTypeCommand(id, request), ct);
        return NoContent();
    }

    // ---- Setup: holidays ----

    [HttpGet("holidays")]
    public async Task<ActionResult<IReadOnlyList<HolidayDto>>> Holidays([FromQuery] short year, CancellationToken ct)
        => Ok(await mediator.Send(new GetHolidaysQuery(year == 0 ? (short)DateTime.UtcNow.Year : year), ct));

    [HttpPost("holidays")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> CreateHoliday([FromBody] SaveHolidayRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateHolidayCommand(request), ct);
        return Created($"api/leave/holidays/{id}", new { id });
    }

    [HttpPut("holidays/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> UpdateHoliday(Guid id, [FromBody] SaveHolidayRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateHolidayCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("holidays/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> DeleteHoliday(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteHolidayCommand(id), ct);
        return NoContent();
    }

    // ---- Setup: policies ----

    [HttpGet("policies")]
    public async Task<ActionResult<IReadOnlyList<LeavePolicyDto>>> Policies(CancellationToken ct)
        => Ok(await mediator.Send(new GetLeavePoliciesQuery(), ct));

    [HttpPost("policies")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> CreatePolicy([FromBody] SaveLeavePolicyRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateLeavePolicyCommand(request), ct);
        return Created($"api/leave/policies/{id}", new { id });
    }

    [HttpPut("policies/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] SaveLeavePolicyRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateLeavePolicyCommand(id, request), ct);
        return NoContent();
    }

    // ---- Setup: approval chain ----

    [HttpGet("approval-settings")]
    public async Task<ActionResult<LeaveApprovalSettingsDto>> ApprovalSettings(CancellationToken ct)
        => Ok(await mediator.Send(new GetLeaveApprovalSettingsQuery(), ct));

    [HttpPut("approval-settings")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> SaveApprovalSettings([FromBody] SaveLeaveApprovalSettingsCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }

    public sealed record CommentRequest(string? Comment);
}
