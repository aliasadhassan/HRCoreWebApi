namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Attendance;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Attendance setup page (Shifts / Policies / Devices tabs). Dekhna: koi bhi; badalna: settings.manage.</summary>
[ApiController]
[Authorize]
[Route("api/attendance/setup")]
public sealed class AttendanceSetupController(ISender mediator) : ControllerBase
{
    [HttpGet("shifts")]
    public async Task<ActionResult<IReadOnlyList<ShiftDto>>> GetShifts([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await mediator.Send(new GetShiftsQuery(includeInactive), ct));

    [HttpPost("shifts")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> CreateShift([FromBody] SaveShiftRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateShiftCommand(request), ct);
        return Created($"api/attendance/setup/shifts/{id}", new { id });
    }

    [HttpPut("shifts/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> UpdateShift(Guid id, [FromBody] SaveShiftRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateShiftCommand(id, request), ct);
        return NoContent();
    }

    [HttpGet("policies")]
    public async Task<ActionResult<IReadOnlyList<AttendancePolicyDto>>> GetPolicies(CancellationToken ct)
        => Ok(await mediator.Send(new GetAttendancePoliciesQuery(), ct));

    [HttpPost("policies")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> CreatePolicy([FromBody] SaveAttendancePolicyRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateAttendancePolicyCommand(request), ct);
        return Created($"api/attendance/setup/policies/{id}", new { id });
    }

    [HttpPut("policies/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] SaveAttendancePolicyRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateAttendancePolicyCommand(id, request), ct);
        return NoContent();
    }

    [HttpGet("devices")]
    [HasPermission(Permissions.SettingsView)]
    public async Task<ActionResult<IReadOnlyList<AttendanceDeviceDto>>> GetDevices(CancellationToken ct)
        => Ok(await mediator.Send(new GetAttendanceDevicesQuery(), ct));

    [HttpPost("devices")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> CreateDevice([FromBody] SaveAttendanceDeviceRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateAttendanceDeviceCommand(request), ct);
        return Created($"api/attendance/setup/devices/{id}", new { id });
    }

    [HttpPut("devices/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> UpdateDevice(Guid id, [FromBody] SaveAttendanceDeviceRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateAttendanceDeviceCommand(id, request), ct);
        return NoContent();
    }
}
