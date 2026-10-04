namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Attendance;
using HR.Employee.API.Application.Common.Models;
using HR.Employee.API.Domain.Attendance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Attendance page: Timesheet, Roster, Overtime, Requests tabs + "clock in/out" card.
/// Kaun kya kar sakta hai, handlers mein (AttendanceAccess) — apna data sab, team manager, sab ka HR.
/// </summary>
[ApiController]
[Authorize]
[Route("api/attendance")]
public sealed class AttendanceController(ISender mediator) : ControllerBase
{
    // ---- Me / clock ----

    [HttpGet("me/today")]
    public async Task<ActionResult<MyTodayDto>> MyToday(CancellationToken ct)
        => Ok(await mediator.Send(new GetMyTodayQuery(), ct));

    [HttpPost("me/clock")]
    public async Task<ActionResult<MyTodayDto>> Clock([FromBody] ClockRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new ClockCommand(
            request.Source, request.Latitude, request.Longitude, request.Note,
            HttpContext.Connection.RemoteIpAddress?.ToString()), ct));

    // ---- Timesheet ----

    [HttpGet("timesheet")]
    public async Task<ActionResult<PagedResult<AttendanceDayDto>>> Timesheet([FromQuery] GetTimesheetQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpGet("summary")]
    public async Task<ActionResult<AttendanceSummaryDto>> Summary([FromQuery] DateOnly date, [FromQuery] Guid? locationId, CancellationToken ct)
        => Ok(await mediator.Send(new GetAttendanceSummaryQuery(date, locationId), ct));

    [HttpGet("days/{id:guid}")]
    public async Task<ActionResult<AttendanceDayDetailDto>> Day(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetAttendanceDayQuery(id), ct));

    [HttpPost("punches")]
    public async Task<IActionResult> AddManualPunch([FromBody] AddManualPunchCommand command, CancellationToken ct)
        => Ok(new { dayId = await mediator.Send(command, ct) });

    [HttpPost("days/{dayId:guid}/punches/{punchId:guid}/ignore")]
    public async Task<IActionResult> IgnorePunch(Guid dayId, Guid punchId, [FromBody] ReasonRequest request, CancellationToken ct)
    {
        await mediator.Send(new IgnorePunchCommand(dayId, punchId, request.Reason), ct);
        return NoContent();
    }

    [HttpPut("days/{id:guid}/status")]
    public async Task<IActionResult> OverrideDay(Guid id, [FromBody] OverrideDayRequest request, CancellationToken ct)
    {
        await mediator.Send(new OverrideAttendanceDayCommand(id, request.Status, request.Remarks), ct);
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<ActionResult<ImportPunchesResult>> Import([FromBody] ImportPunchesCommand command, CancellationToken ct)
        => Ok(await mediator.Send(command, ct));

    [HttpPost("process")]
    public async Task<IActionResult> Process([FromBody] ProcessAttendanceCommand command, CancellationToken ct)
        => Ok(new { employees = await mediator.Send(command, ct) });

    // ---- Roster ----

    [HttpGet("roster")]
    public async Task<ActionResult<IReadOnlyList<RosterRowDto>>> Roster([FromQuery] GetRosterQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpGet("roster/assignments")]
    public async Task<ActionResult<IReadOnlyList<ShiftAssignmentDto>>> Assignments(
        [FromQuery] Guid? employeeId, [FromQuery] Guid? shiftId, [FromQuery] bool currentOnly = true, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetShiftAssignmentsQuery(employeeId, shiftId, currentOnly), ct));

    [HttpPost("roster/assignments")]
    public async Task<IActionResult> Assign([FromBody] AssignShiftCommand command, CancellationToken ct)
        => Ok(new { assigned = await mediator.Send(command, ct) });

    [HttpDelete("roster/assignments/{id:guid}")]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteShiftAssignmentCommand(id), ct);
        return NoContent();
    }

    [HttpPut("roster/entries")]
    public async Task<IActionResult> SetEntry([FromBody] SetRosterEntryCommand command, CancellationToken ct)
        => Ok(new { id = await mediator.Send(command, ct) });

    [HttpDelete("roster/entries")]
    public async Task<IActionResult> ClearEntry([FromQuery] Guid employeeId, [FromQuery] DateOnly workDate, CancellationToken ct)
    {
        await mediator.Send(new ClearRosterEntryCommand(employeeId, workDate), ct);
        return NoContent();
    }

    // ---- Requests (corrections, WFH, on duty) + Overtime ----

    [HttpGet("requests")]
    public async Task<ActionResult<PagedResult<AttendanceRequestDto>>> Requests([FromQuery] GetAttendanceRequestsQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpPost("requests")]
    public async Task<IActionResult> Submit([FromBody] SubmitAttendanceRequestCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/attendance/requests/{id}", new { id });
    }

    [HttpPost("requests/overtime")]
    public async Task<IActionResult> SubmitOvertime([FromBody] SubmitOvertimeRequestCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return Created($"api/attendance/requests/{id}", new { id });
    }

    [HttpPost("requests/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRequest request, CancellationToken ct)
    {
        await mediator.Send(new ApproveAttendanceRequestCommand(id, request.Comment, request.ApprovedMinutes), ct);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReasonRequest request, CancellationToken ct)
    {
        await mediator.Send(new RejectAttendanceRequestCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelAttendanceRequestCommand(id), ct);
        return NoContent();
    }

    public sealed record ClockRequest(PunchSource Source, decimal? Latitude, decimal? Longitude, string? Note);
    public sealed record ReasonRequest(string Reason);
    public sealed record OverrideDayRequest(AttendanceStatus Status, string Remarks);
    public sealed record ApproveRequest(string? Comment, short? ApprovedMinutes);
}
