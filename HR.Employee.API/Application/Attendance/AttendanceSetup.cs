namespace HR.Employee.API.Application.Attendance;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Leaves;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Attendance setup page: Shifts, Policies, Devices tabs. Simple CRUD (Locations jaisa).

#region Shifts

public sealed record ShiftDto(
    Guid Id, string Name, string Code, string? Color, TimeOnly StartTime, TimeOnly EndTime, short BreakMinutes,
    byte GraceInMinutes, byte GraceOutMinutes, bool IsFlexible, bool IsActive, bool CrossesMidnight, int NetMinutes,
    int AssignedEmployees);

public sealed record SaveShiftRequest(
    string Name, string Code, string? Color, TimeOnly StartTime, TimeOnly EndTime,
    short BreakMinutes = 60, byte GraceInMinutes = 15, byte GraceOutMinutes = 0, bool IsFlexible = false, bool IsActive = true);

public sealed record GetShiftsQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<ShiftDto>>;
public sealed record CreateShiftCommand(SaveShiftRequest Data) : IRequest<Guid>;
public sealed record UpdateShiftCommand(Guid Id, SaveShiftRequest Data) : IRequest;

public sealed class SaveShiftRequestValidator : AbstractValidator<SaveShiftRequest>
{
    public SaveShiftRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.Color is not null);
        RuleFor(x => x.EndTime).NotEqual(x => x.StartTime).WithMessage("Shift start and end time cannot be the same.");
        RuleFor(x => x.BreakMinutes).InclusiveBetween((short)0, (short)240);
        RuleFor(x => x.GraceInMinutes).LessThanOrEqualTo((byte)120);
        RuleFor(x => x.GraceOutMinutes).LessThanOrEqualTo((byte)120);
    }
}

public sealed class CreateShiftValidator : AbstractValidator<CreateShiftCommand>
{
    public CreateShiftValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveShiftRequestValidator());
}

public sealed class UpdateShiftValidator : AbstractValidator<UpdateShiftCommand>
{
    public UpdateShiftValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveShiftRequestValidator());
}

public sealed class ShiftHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetShiftsQuery, IReadOnlyList<ShiftDto>>,
    IRequestHandler<CreateShiftCommand, Guid>,
    IRequestHandler<UpdateShiftCommand>
{
    public async Task<IReadOnlyList<ShiftDto>> Handle(GetShiftsQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var shifts = await db.Shifts.AsNoTracking()
            .Where(x => request.IncludeInactive || x.IsActive)
            .OrderBy(x => x.StartTime).ThenBy(x => x.Name)
            .ToListAsync(ct);
        var counts = await db.ShiftAssignments.AsNoTracking()
            .Where(a => a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .GroupBy(a => a.ShiftId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        return shifts.Select(s => new ShiftDto(
            s.Id, s.Name, s.Code, s.Color, s.StartTime, s.EndTime, s.BreakMinutes, s.GraceInMinutes, s.GraceOutMinutes,
            s.IsFlexible, s.IsActive, s.CrossesMidnight, s.NetMinutes, counts.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<Guid> Handle(CreateShiftCommand request, CancellationToken ct)
    {
        var d = request.Data;
        await EnsureCodeIsFreeAsync(d.Code, null, ct);
        var shift = Shift.Create(currentUser.RequireTenantId(), d.Name, d.Code, d.Color, d.StartTime, d.EndTime,
                                 d.BreakMinutes, d.GraceInMinutes, d.GraceOutMinutes, d.IsFlexible);
        if (!d.IsActive)
            shift.Deactivate();

        db.Shifts.Add(shift);
        await db.SaveChangesAsync(ct);
        return shift.Id;
    }

    public async Task Handle(UpdateShiftCommand request, CancellationToken ct)
    {
        var d = request.Data;
        var shift = await db.Shifts.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
                    ?? throw new NotFoundException("Shift", request.Id);

        await EnsureCodeIsFreeAsync(d.Code, shift.Id, ct);
        shift.Update(d.Name, d.Code, d.Color, d.StartTime, d.EndTime, d.BreakMinutes, d.GraceInMinutes, d.GraceOutMinutes, d.IsFlexible);
        if (d.IsActive) shift.Activate(); else shift.Deactivate();

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureCodeIsFreeAsync(string code, Guid? excludeId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await db.Shifts.AnyAsync(x => x.Code == normalized && x.Id != excludeId, ct))
            throw new ConflictException($"Shift code '{normalized}' is already in use.");
    }
}

#endregion

#region Policies

public sealed record AttendancePolicyDto(
    Guid Id, string Name, Guid? LocationId, string? LocationName, bool IsActive,
    short FullDayMinutes, short HalfDayMinutes, byte? LatesPerHalfDay,
    ClockInMethods AllowedMethods, bool RequireGeofence, decimal? GeoLatitude, decimal? GeoLongitude, short? GeoRadiusMeters,
    ApproverType RequestApprover, byte CorrectionWindowDays, byte? MaxCorrectionsPerMonth,
    bool OvertimeEnabled, short OvertimeMinMinutes, short? OvertimeMaxMinutesPerDay, bool OvertimeRequiresApproval,
    decimal OvertimeRateWorkday, decimal OvertimeRateWeeklyOff, decimal OvertimeRateHoliday);

public sealed record SaveAttendancePolicyRequest(
    string Name, Guid? LocationId, bool IsActive,
    short FullDayMinutes, short HalfDayMinutes, byte? LatesPerHalfDay,
    ClockInMethods AllowedMethods, bool RequireGeofence, decimal? GeoLatitude, decimal? GeoLongitude, short? GeoRadiusMeters,
    ApproverType RequestApprover, byte CorrectionWindowDays, byte? MaxCorrectionsPerMonth,
    bool OvertimeEnabled, short OvertimeMinMinutes, short? OvertimeMaxMinutesPerDay, bool OvertimeRequiresApproval,
    decimal OvertimeRateWorkday, decimal OvertimeRateWeeklyOff, decimal OvertimeRateHoliday);

public sealed record GetAttendancePoliciesQuery : IRequest<IReadOnlyList<AttendancePolicyDto>>;
public sealed record CreateAttendancePolicyCommand(SaveAttendancePolicyRequest Data) : IRequest<Guid>;
public sealed record UpdateAttendancePolicyCommand(Guid Id, SaveAttendancePolicyRequest Data) : IRequest;

public sealed class SaveAttendancePolicyRequestValidator : AbstractValidator<SaveAttendancePolicyRequest>
{
    public SaveAttendancePolicyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.HalfDayMinutes).GreaterThan((short)0).LessThan(x => x.FullDayMinutes);
        RuleFor(x => x.FullDayMinutes).InclusiveBetween((short)1, (short)1440);
        RuleFor(x => x.AllowedMethods).NotEqual((ClockInMethods)0).WithMessage("At least one clock-in method is required.");
        RuleFor(x => x.RequestApprover).IsInEnum();
        RuleFor(x => x.CorrectionWindowDays).InclusiveBetween((byte)1, (byte)90);
        RuleFor(x => x.OvertimeRateWorkday).InclusiveBetween(1m, 5m);
        RuleFor(x => x.OvertimeRateWeeklyOff).InclusiveBetween(1m, 5m);
        RuleFor(x => x.OvertimeRateHoliday).InclusiveBetween(1m, 5m);
        When(x => x.RequireGeofence, () =>
        {
            RuleFor(x => x.GeoLatitude).NotNull().InclusiveBetween(-90m, 90m);
            RuleFor(x => x.GeoLongitude).NotNull().InclusiveBetween(-180m, 180m);
            RuleFor(x => x.GeoRadiusMeters).NotNull().GreaterThan((short)0);
        });
    }
}

public sealed class CreateAttendancePolicyValidator : AbstractValidator<CreateAttendancePolicyCommand>
{
    public CreateAttendancePolicyValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveAttendancePolicyRequestValidator());
}

public sealed class UpdateAttendancePolicyValidator : AbstractValidator<UpdateAttendancePolicyCommand>
{
    public UpdateAttendancePolicyValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveAttendancePolicyRequestValidator());
}

public sealed class AttendancePolicyHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetAttendancePoliciesQuery, IReadOnlyList<AttendancePolicyDto>>,
    IRequestHandler<CreateAttendancePolicyCommand, Guid>,
    IRequestHandler<UpdateAttendancePolicyCommand>
{
    public async Task<IReadOnlyList<AttendancePolicyDto>> Handle(GetAttendancePoliciesQuery request, CancellationToken ct)
        => await db.AttendancePolicies.AsNoTracking()
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.LocationId != null).ThenBy(p => p.Name)
            .Select(p => new AttendancePolicyDto(
                p.Id, p.Name, p.LocationId,
                db.Locations.Where(l => l.Id == p.LocationId).Select(l => l.Name).FirstOrDefault(),
                p.IsActive, p.FullDayMinutes, p.HalfDayMinutes, p.LatesPerHalfDay,
                p.AllowedMethods, p.RequireGeofence, p.GeoLatitude, p.GeoLongitude, p.GeoRadiusMeters,
                p.RequestApprover, p.CorrectionWindowDays, p.MaxCorrectionsPerMonth,
                p.OvertimeEnabled, p.OvertimeMinMinutes, p.OvertimeMaxMinutesPerDay, p.OvertimeRequiresApproval,
                p.OvertimeRateWorkday, p.OvertimeRateWeeklyOff, p.OvertimeRateHoliday))
            .ToListAsync(ct);

    public async Task<Guid> Handle(CreateAttendancePolicyCommand request, CancellationToken ct)
    {
        var d = request.Data;
        await EnsureLocationAsync(d.LocationId, ct);
        if (d.IsActive)
            await EnsureNoOtherActiveAsync(d.LocationId, null, ct);

        var policy = AttendancePolicy.Create(currentUser.RequireTenantId(), d.Name, d.LocationId);
        Apply(policy, d);
        db.AttendancePolicies.Add(policy);
        await db.SaveChangesAsync(ct);
        return policy.Id;
    }

    public async Task Handle(UpdateAttendancePolicyCommand request, CancellationToken ct)
    {
        var d = request.Data;
        var policy = await db.AttendancePolicies.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
                     ?? throw new NotFoundException("Attendance policy", request.Id);
        if (policy.LocationId != d.LocationId)
            throw new ConflictException("A policy's location cannot be changed. Create a new policy for the other location.");
        if (d.IsActive)
            await EnsureNoOtherActiveAsync(d.LocationId, policy.Id, ct);

        policy.Rename(d.Name);
        Apply(policy, d);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(AttendancePolicy policy, SaveAttendancePolicyRequest d)
    {
        policy.SetDayRules(d.FullDayMinutes, d.HalfDayMinutes, d.LatesPerHalfDay);
        policy.SetClockIn(d.AllowedMethods, d.RequireGeofence, d.GeoLatitude, d.GeoLongitude, d.GeoRadiusMeters);
        policy.SetRequestRules(d.RequestApprover, d.CorrectionWindowDays, d.MaxCorrectionsPerMonth);
        policy.SetOvertime(d.OvertimeEnabled, d.OvertimeMinMinutes, d.OvertimeMaxMinutesPerDay, d.OvertimeRequiresApproval,
                           d.OvertimeRateWorkday, d.OvertimeRateWeeklyOff, d.OvertimeRateHoliday);
        if (d.IsActive) policy.Activate(); else policy.Deactivate();
    }

    private async Task EnsureLocationAsync(Guid? locationId, CancellationToken ct)
    {
        if (locationId is { } id && !await db.Locations.AnyAsync(l => l.Id == id, ct))
            throw new NotFoundException("Location", id);
    }

    private async Task EnsureNoOtherActiveAsync(Guid? locationId, Guid? excludeId, CancellationToken ct)
    {
        if (await db.AttendancePolicies.AnyAsync(p => p.IsActive && p.LocationId == locationId && p.Id != excludeId, ct))
            throw new ConflictException(locationId is null
                ? "There is already an active default policy. Deactivate it first."
                : "This location already has an active policy. Deactivate it first.");
    }
}

#endregion

#region Devices

public sealed record AttendanceDeviceDto(
    Guid Id, string Name, string SerialNumber, string? Vendor, Guid LocationId, string LocationName,
    bool HasApiKey, DateTime? LastSyncedAt, bool IsActive);

public sealed record SaveAttendanceDeviceRequest(string Name, string SerialNumber, string? Vendor, Guid LocationId, bool IsActive = true);

public sealed record GetAttendanceDevicesQuery : IRequest<IReadOnlyList<AttendanceDeviceDto>>;
public sealed record CreateAttendanceDeviceCommand(SaveAttendanceDeviceRequest Data) : IRequest<Guid>;
public sealed record UpdateAttendanceDeviceCommand(Guid Id, SaveAttendanceDeviceRequest Data) : IRequest;

public sealed class SaveAttendanceDeviceRequestValidator : AbstractValidator<SaveAttendanceDeviceRequest>
{
    public SaveAttendanceDeviceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SerialNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Vendor).MaximumLength(50);
        RuleFor(x => x.LocationId).NotEmpty();
    }
}

public sealed class CreateAttendanceDeviceValidator : AbstractValidator<CreateAttendanceDeviceCommand>
{
    public CreateAttendanceDeviceValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveAttendanceDeviceRequestValidator());
}

public sealed class UpdateAttendanceDeviceValidator : AbstractValidator<UpdateAttendanceDeviceCommand>
{
    public UpdateAttendanceDeviceValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveAttendanceDeviceRequestValidator());
}

public sealed class AttendanceDeviceHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetAttendanceDevicesQuery, IReadOnlyList<AttendanceDeviceDto>>,
    IRequestHandler<CreateAttendanceDeviceCommand, Guid>,
    IRequestHandler<UpdateAttendanceDeviceCommand>
{
    public async Task<IReadOnlyList<AttendanceDeviceDto>> Handle(GetAttendanceDevicesQuery request, CancellationToken ct)
        => await db.AttendanceDevices.AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new AttendanceDeviceDto(
                d.Id, d.Name, d.SerialNumber, d.Vendor, d.LocationId,
                db.Locations.Where(l => l.Id == d.LocationId).Select(l => l.Name).First(),
                d.ApiKeyHash != null, d.LastSyncedAt, d.IsActive))
            .ToListAsync(ct);

    public async Task<Guid> Handle(CreateAttendanceDeviceCommand request, CancellationToken ct)
    {
        var d = request.Data;
        await EnsureValidAsync(d, null, ct);
        var device = AttendanceDevice.Create(currentUser.RequireTenantId(), d.Name, d.SerialNumber, d.Vendor, d.LocationId);
        if (!d.IsActive)
            device.Deactivate();

        db.AttendanceDevices.Add(device);
        await db.SaveChangesAsync(ct);
        return device.Id;
    }

    public async Task Handle(UpdateAttendanceDeviceCommand request, CancellationToken ct)
    {
        var d = request.Data;
        var device = await db.AttendanceDevices.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
                     ?? throw new NotFoundException("Device", request.Id);
        await EnsureValidAsync(d, device.Id, ct);

        device.Update(d.Name, d.SerialNumber, d.Vendor, d.LocationId);
        if (d.IsActive) device.Activate(); else device.Deactivate();
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureValidAsync(SaveAttendanceDeviceRequest d, Guid? excludeId, CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == d.LocationId, ct))
            throw new NotFoundException("Location", d.LocationId);

        var serial = d.SerialNumber.Trim().ToUpperInvariant();
        if (await db.AttendanceDevices.AnyAsync(x => x.SerialNumber == serial && x.Id != excludeId, ct))
            throw new ConflictException($"A device with serial '{serial}' is already registered.");
    }
}

#endregion
